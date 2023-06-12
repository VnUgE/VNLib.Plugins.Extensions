/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Extensions.Loading
* File: IAsyncLazy.cs 
*
* IAsyncLazy.cs is part of VNLib.Plugins.Extensions.Loading which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Extensions.Loading is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Extensions.Loading is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/


using System;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace VNLib.Plugins.Extensions.Loading
{
    /// <summary>
    /// Represents an asynchronous lazy operation. with non-blocking access to the target value.
    /// </summary>
    /// <typeparam name="T">The result type</typeparam>
    public interface IAsyncLazy<T>
    {
        /// <summary>
        /// Gets a value indicating whether the asynchronous operation has completed.
        /// </summary>
        bool Completed { get; }

        /// <summary>
        /// Gets a task that represents the asynchronous operation.
        /// </summary>
        /// <returns></returns>
        TaskAwaiter<T> GetAwaiter();

        /// <summary>
        /// Gets the target value of the asynchronous operation without blocking. 
        /// If the operation failed, throws an exception that caused the failure. 
        /// If the operation has not completed, throws an exception.
        /// </summary>
        T Value { get; }
    }

    /// <summary>
    /// Extension methods for <see cref="IAsyncLazy{T}"/>
    /// </summary>
    public static class AsyncLazyExtensions
    {
        /// <summary>
        /// Gets an <see cref="IAsyncLazy{T}"/> wrapper for the specified <see cref="Task{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="task"></param>
        /// <returns>The async operation task wrapper</returns>
        public static IAsyncLazy<T> AsLazy<T>(this Task<T> task) => new AsyncLazy<T>(task);

        /// <summary>
        /// Tranforms one lazy operation into another using the specified handler
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <typeparam name="TResult">The resultant type</typeparam>
        /// <param name="lazy"></param>
        /// <param name="handler">The function that will peform the transformation of the lazy result</param>
        /// <returns>A new <see cref="IAsyncLazy{T}"/> that returns the transformed type</returns>
        public static IAsyncLazy<TResult> Transform<T, TResult>(this IAsyncLazy<T> lazy, Func<T, TResult> handler)
        {
            _ = lazy ?? throw new ArgumentNullException(nameof(lazy));
            _ = handler ?? throw new ArgumentNullException(nameof(handler));

            //Await the lazy task, then pass the result to the handler
            static async Task<TResult> OnResult(IAsyncLazy<T> lazy, Func<T, TResult> cb)
            {
                T result = await lazy;
                return cb(result);
            }

            return OnResult(lazy, handler).AsLazy();
        }

#nullable disable

        private sealed class AsyncLazy<T> : IAsyncLazy<T>
        {
            private readonly Task<T> _task;

            private T _result;

            public AsyncLazy(Task<T> task)
            {
                _task = task ?? throw new ArgumentNullException(nameof(task));
                _ = task.ContinueWith(SetResult, TaskScheduler.Default);
            }

            ///<inheritdoc/>
            public bool Completed => _task.IsCompleted;

            ///<inheritdoc/>
            public T Value
            {
                get
                {
                    if (_task.IsCompletedSuccessfully)
                    {
                        return _result;
                    }
                    else if(_task.IsFaulted)
                    {
                        //Compress and raise exception from result
                        return _task.GetAwaiter().GetResult();
                    }
                    else
                    {
                        throw new InvalidOperationException("The asynchronous operation has not completed.");
                    }
                }
            }

            /*
             * Only set the result if the task completed successfully.
             */
            private void SetResult(Task<T> task)
            {
                if (task.IsCompletedSuccessfully)
                {
                    _result = task.Result;
                }
            }

            ///<inheritdoc/>
            public TaskAwaiter<T> GetAwaiter() => _task.GetAwaiter();
        }
#nullable enable

    }
}
