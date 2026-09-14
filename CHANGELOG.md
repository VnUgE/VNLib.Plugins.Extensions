# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Add IPC shared object export and consumer extension library - (ipc) [eb04529](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=eb0452990c87284e50de039a08880024cfff9990), [58ff893](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=58ff893a1a5153d5ffad86b62dba18a94d7afcd), [42d03e5](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=42d03e52559aafc49e965feb469c8e6c9504f591), [589b271](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=589b2713f5168fcd030ea0dc6b3ca64c990c8a14)
- Add unit test coverage for IPC export bridges and the publish worker - (ipc) [67e9bab](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=67e9bab5ab5c2b62d35ad70390bad7ff6bff0231)
- Add PluginConfigStore struct-based configuration manager - (config) [76d0fd0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=76d0fd014e9766c2cf17b2d31c93d0cffb04cd4e)
- Add dependency singleton cache utilities - (loading) [b9e47de](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=b9e47de97e346bc9a4b78344625a90d7a9174d2a)
- Add Validate.Matches regex utility and unit tests - (loading) [2e92c8c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=2e92c8cd4fdad7cfb781a2c27fe7c65387e5e77a), [29a1bc9](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=29a1bc9e1da568383dd2f82cacad6167ef2817b8)
- Support nullable strings for validator extension functions - (validation) [2da96a7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=2da96a7b8f0001bb0e9e9ce7416332f9aa34c43c)
- Add vault environment variable support - (secrets) [27fb538](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=27fb5382d80d9bcfb4c65974bbae20c5e7b8ccbc)
- Add middleware overload to initialize types at runtime - (routing) [233787d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=233787d04819d771f50b3d2153e5a90058762ccd)
- Add MVC static routing extensions - (routing) [711b12f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=711b12fa249cba9effecd4e722dd8d460d083659), [1229ed7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=1229ed75549de1c56aaee42c921acbd96c4d4c9b)
- Add unit test scaffolding and coverage for loading, config, and SQL providers - (tests) [8e49c3c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=8e49c3c861984e3114abd1b2459aa2dd5509d8e5), [2ce86c3](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=2ce86c35165a09ca2895d6ecdd1b8587cf98390), [321968c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=321968c5587937f2769b4fe9c1caefd1d861ef11), [d4b7874](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=d4b7874e2b7e6462a0f0cc228ed3d68389016e32), [663df79](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=663df792339932efa249ed8bfbc04b50829aef5a)

### Changed

- **Breaking Change:** Rework secrets namespace with scheme readers and secret store improvements - (secrets) [69ebce8](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=69ebce88dd1f45a2b548510a445663cc6964ff25), [d52772c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=d52772c010b10357d194239056d19c0d22d414fe)
- **Breaking Change:** Extract LoadingExtensions into focused extension classes - (loading) [9e96197](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=9e961976989d5eead3a7751532c6ff32bb3ddf62)
- **Breaking Change:** Replace mvc route protection with guard interceptors - (loading) [f00cdcb](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=f00cdcbd0e5b8383c3f9ea00aa90e4ee06e3c3c3)
- **Breaking Change:** Deprecate dynamic type inference and obsolete ConcreteType exceptions - (loading) [a0e3d07](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=a0e3d07113f8cdf7134706b44182b3224eb73f16), [3bad277](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=3bad2775d4152dc11a30781acddb9bd3a84b7420)
- **Breaking Change:** Remove PluginBase.ProcessHostCommand console handler after upstream core update - (loading) [44065e4](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=44065e4df074ff717925a2381f96376c2a64af69)
- **Breaking Change:** Fix spelling of EventManagement, IIntervalSchedulable, and IntervalResolutionType - (events) [f2cc559](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=f2cc55982a105e0d8ee2117f76fe195d130fd9ed)
- **Breaking Change:** Correct typos on Sql interfaces - (sql) [9151f51](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=9151f51917263cdc69d6ec8d4c11eeb744554e08)
- Expand and simplify configuration helpers - (config) [940f151](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=940f151f2f4e708282deb5569cffe5b0935f5c3b), [c8567e5](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=c8567e58dc1d4135da1f6cefa6fa66af5fcd7b19), [9bc2480](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=9bc24801735884e0c03aa00e83804448c466bdf2), [db5747a](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=db5747a20600a2e2c5e8d915cf0bdbe4ec6df6a2)
- Improve validation in multiple places - (loading) [e23ca45](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=e23ca45b875c66b036e7ad58cbef239706400374), [9487d0b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=9487d0ba923fc189158acd345e7b5ec5a1ed3eaa), [c3c86c8](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=c3c86c8774942c20f1e774b3a8fb6cd963a0352b)
- Improve secret readers validation and docs - (secrets) [901fda3](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=901fda3edc7418d38f367333aead42fc7dd56ab)
- Improve assembly cache table garbage collection - (loading) [b55e96d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=b55e96dbb6346621e02920d4ce3826436d515e90)
- Change route variable substitution default path - (routing) [e345dfa](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=e345dfa4e295265d060afe4d5ffb8d5702805b79)
- Define JSON vault config object - (secrets) [7b50f50](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=7b50f500ec30fd180359c7efa1cac85018cb700d)
- Obsolete S3Config type - (loading) [caf15b0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=caf15b0fad1bb3de696f105a8423632a84061d2b)
- Obsolete Password() validator extension method - (validation) [4fd16f7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=4fd16f7b373546b1720f5a276957ef97e0b81a07)
- Obsolete GetSecretAsync() and TryGetSecretAsync() extension methods - (secrets) [d47e800](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=d47e800145e60d5f98f90917dd559169ccc43e63)
- Update vnlib.core to v0.1.5 - (deps) [6e4b239](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=6e4b2392535d21c5a63d7ace935b362c7218f121)
- Update EFCore to 8.0.24 - (deps) [f20f6da](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=f20f6da7071758a2156ac1f8109ae0533b878efd)
- Update EFCore SQL Server to 8.0.24 - (deps) [d5ca6e7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=d5ca6e7c7753f49db2f69ca4c2bd1b0af647c2f2)
- Update EFCore Sqlite to 8.0.24 - (deps) [aed6d04](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=aed6d04211722e09a3c01f61ca3b3477ad65aeab)
- Update FluentValidation to 12.1.1 - (deps) [1aa460d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=1aa460d696b1c168d5bc35bca7a7f93969953840)
- Update MSTest packages to v4 and test dependencies to latest stable versions - (deps) [512f653](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=512f653b38e60428f304cf3c54a3181da14a6649), [b4c191b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=b4c191bacade3ed7235074f1bc4b504de62377bf)
- Remove ErrorProne.NET analyzers breaking builds - (deps) [cd46d58](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=cd46d58f0adccaaa78851895fed6cc270c9c70ec)
- Centralize MSBuild config via Directory.Build.props and enable warnings as errors - (build) [a70ce82](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=a70ce827239c19398e73700c7ce6135225e6a4e5), [f9d7e39](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=f9d7e39e4bfedde1ff473f10fb57ffc986142084)
- Improve module and default taskfiles for vnbuild and update solution file - (build) [a971cc5](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=a971cc55bd270da01e8ef41f77b880e839f6eb68), [a93d5c8](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=a93d5c89d329ee91341a9209a0e8ac4b83cfebac)
- Rename mise tasks to ci- prefix, move mise.toml to root, and update CI jobs - (ci) [1e33ff9](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=1e33ff9bf0394370c43e47fd8aca6b9642236dc2), [cf45f1d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=cf45f1d015c1d8bb6cd1fb288802fb1f5cd487ba), [7932ee2](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=7932ee24fd62041df0e5be66c9c91de85dccbe1a), [728cff1](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=728cff138015b7b7732e71ee5eaf4ae4139bdb4c), [e970d70](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=e970d700fd113294518e571bf3a5648d00be2ef2)
- Update copyright year to 2026 - (housekeeping) [fb5e6c2](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=fb5e6c26067ca9430d99e2c409aa2f5207f6d893)
- Review and correct documentation and typos across library packages - (docs) [e6fb05e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=e6fb05e6585b4767af2e3b3f996dd497d32aeb7c), [ce8883c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=ce8883ca17c98696d604a386c6fd3fca521400f1)

### Fixed

- Fix circular dependency causing stack overflow - (loading) [d36da1b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=d36da1b53770f6c9a0a396354a75fde1c48e0cbf)
- Allow empty or whitespace raw string values on internal resolver - (loading) [9f64c50](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=9f64c506ab1bef8b6847cd66fdca163210a7a586)
- Fix race condition in internal IAsyncLazy implementation - (loading) [31e0730](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=31e07305f5fff4a27e16e1f730fa16b0cadb0ffe)
- Fix missing rename in events merge - (events) [172c340](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=172c340e42182078870f8065db799049824a5dfc)
- Cache property store assembly - (loading) [fce0f9d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=fce0f9dbbb4c8094e71758cb60af186c0933178e)
- Fix mysql connection string password assignment - (mysql) [654f304](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=654f304cc6a3e17616da6e209220871915860ef1)
- Treat empty string equal to null and short circuit to empty secret - (secrets) [c7f6639](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=c7f6639113986d2e2ae17cac9a75b5f3fbb86c53)
- Add cancellation token protection for Env variable secret reader - (secrets) [096880e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=096880e1afbed4cb9d03771cf9831fc99001e9b3)
- Fix TOCTOU bug in observed work scheduler - (loading) [a0a5b86](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=a0a5b869f27b72c079eba2977cf183225c4753de)
- Fix unexpected raw secret erasure - (secrets) [4237750](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=42377501eb066f99c8e9d3f4a89b7595329e519b)
- Fix error raised when managed password type disposed - (passwords) [21c6c85](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=21c6c85f540740ac29536a7091346a731aa85148)
- Defer vault loading until a secret actually needs it - (secrets) [69f13e4](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=69f13e43dfdd8069459800ccc3039f45fc884814)
- Allow whitespace in config variable substitution for routes - (loading) [0b212b6](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=0b212b666b937a43bab7b62d68ec51d47b73b785)
- Fix endpoint initialization - (routing) [3f6a803](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=3f6a80306935afbd9cc74bd2bec83977a2ae12ae)
- Fix null max length column field in DbCommandHelpers - (sql) [f51155d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=f51155da68738e25e4c9c6b5f9e9242eebd655d2)
- Add null check to CreateScope() logging extension method - (loading) [33aa57e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=33aa57eda49d95f00f30675ee7340dadcadec0c7)
- Fix AsyncIntervalAttribute.Hours property - (events) [3811329](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=3811329a4e4f6524a97dda4d1fe1a25e8708b37a)
- Add try/finally to avoid environment pollution on test failure - (tests) [1090d52](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=1090d52fbfdb9c8773664e2def2424ee08121979)
- Fix tests to solidify file and dynamic type resolution refactor - (tests) [149c2d7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=149c2d7de897a7bf94de61ec0488f82124ec72a9)

### Performance

- Add .ConfigureAwait(false) to all async calls in library - (loading) [da57318](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=da57318fa7eff09c44d4b55ecfe99ddd873e8321)

### Removed

- **Breaking Change:** Remove ManagedPasswordHashing, UserManager, and UserPassValResult - (passwords) [b79c411](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=b79c411de04722689429e2f5af226f01239f8596)
- **Breaking Change:** Remove local IAsyncLazy interface, use VNLib.Utils.Async instead - (loading) [2aae1ae](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=2aae1ae8d9991d57cd76e99d8c6cc40d214be494), [66d29b9](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=66d29b9863fabfa025ad123ead630dfae913b229)
- Remove previously obsoleted APIs and fix build errors from removed members - (loading) [df957f5](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=df957f5e9e088fc0a46ec4760956d37343a655a7)

## [0.1.5] - 2026-01-31

### Added

- Support nullable strings for validator extension functions - (validation) [2da96a7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=2da96a7b8f0001bb0e9e9ce7416332f9aa34c43c)

### Changed

- Update mstest packages to v4+ and refactor analyzer recomendations - (deps) [512f653](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=512f653b38e60428f304cf3c54a3181da14a6649)
- Update vnlib.core to v0.1.5 - (deps) [6e4b239](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=6e4b2392535d21c5a63d7ace935b362c7218f121)
- Update FluentValidation to version 12.1.1 - (deps) [1aa460d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=1aa460d696b1c168d5bc35bca7a7f93969953840)
- Update Dotnet EF Core to 8.0.23 - (deps) [6954191](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=6954191d55b0cf8a62a6f5a5b8c5fd868a49270f)
- Update Dotnet EF Core SQL Server to 8.0.23 - (deps) [339c3ac](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=339c3ac2ad1bbea35ace6ac7ee29b7ba8aec1707)
- Update Dotnet EFCore.Sqlite to version 8.0.23 - (deps) [04b8149](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=04b814990a0e16f2257104875fedc6b31013a010)
- Obsolete `Password()` validator extension method - (validation) [4fd16f7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=4fd16f7b373546b1720f5a276957ef97e0b81a07)

## [0.1.4] - 2025-11-22

### Added

- Add PluginConfigStore struct-based configuration manager - (loading) [76d0fd0](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=76d0fd014e9766c2cf17b2d31c93d0cffb04cd4e)

### Changed

- Update vnlib.core to v0.1.4 - (deps) [8766e46](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=8766e46d7e5569307f8d5ba6c574e6361253c0d2)
- Update FluentValidation to v12.1.0 - (deps) [9c9a259](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=9c9a2596223ba76494e16354faa4eb24e43e020d)
- Update EntityFrameworkCore to version 8.0.22 - (deps) [8b5a9b6](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=8b5a9b6f9930db2e5cdc91274e33f3541c6a09cb)
- Update Microsoft.EntityFrameworkCore.Sqlite to 8.0.22 - (deps) [cd61acd](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=cd61acd48390a0b1434589f3c113dd476c38e806)
- Update EF Core SQLServer package to version 8.0.22 - (deps) [2771f1b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=2771f1b55ca81328f87274eb4b34d585b75dbcf2)
- Migrate ConfigurationExtensions to new store api and move file to Configuration from top-level. - (loading) [32ea95d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=32ea95d3f6ae4f61d675f4c015ebde2a0c2090f5)
- Obsolete `GetSecretAsync()` and `TryGetSecretAsync()` extension methods. - (loading) [d47e800](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=d47e800145e60d5f98f90917dd559169ccc43e63)
- Obsolete `[Try]GetSecret[Async]()` in favor of `Secrets().[Try]Get[Async]()` secrets helper functions - (loading) [ab51d7c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=ab51d7cad36305e29a24c7b1af804d910313f349)

### Fixed

- Scaffold empty unit testing classes for extension libraries - [8e49c3c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=8e49c3c861984e3114abd1b2459aa2dd5509d8e5)
- Add dynamic SQL library loading smoke tests - [2ce86c3](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=2ce86c35165a09ca2895d6ecdd1b8587ca5806ff)
- Flush all connection pools when plugin unloads - (sqlite) [eeb4b87](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=eeb4b87d764611c755009b15f40829b156f097d9)
- Add null check ot `CreateScope()` logging extension method - (loading) [33aa57e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=33aa57eda49d95f00f30675ee7340dadcadec0c7)
- Added tests for new pluging configuration loading api and existing apis. - (loading) [d4b7874](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=d4b7874e2b7e6462a0f0cc228ed3d68389016e32)
- Fix null max length column field in DbCommandHelpers for db creation - (sql) [f51155d](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=f51155da68738e25e4c9c6b5f9e9242eebd655d2)
- Fix mysql connection string password assignment - (mysql) [654f304](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=654f304cc6a3e17616da6e209220871915860ef1)

## [0.1.3] - 2025-10-03

### Changed

- Update vnlib.core to v0.1.3 - (deps) [2ea5967](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=2ea596726c0ef9c819a81441ffbb1327a92e4ed3)
- Updates build scripts to work with Task v3.45 - [e970d70](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=e970d700fd113294518e571bf3a5648d00be2ef2)

## [0.1.2] - 2025-09-20

### Changed

- Update Microsoft.EntityFrameworkCore to version 8.0.20 - (deps) [8182252](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=8182252be0d06c5108a41adc01bbe5e4021b1907)
- Update EntityFrameworkCore.Sqlite package to version 8.0.20 - (deps) [0b4f158](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=0b4f158eee322ee0dfa290a4c31b3867e39189af)
- Update EntityFrameworkCore.SqlServer to 8.0.20 - (deps) [4e50ea5](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=4e50ea51aa34a81c63422d7959c372ac3c851e9b)
- Update vnlib.core to version v0.1.2 - (deps) [ce89b9c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=ce89b9c3ba8c07e641f1790ccad5a22a75f14b56)

## [0.1.2-rc.8] - 2025-09-08

### Changed

- Update vnlib.core to `v0.1.2-rc.10` - (deps) [64ac944](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=64ac9441a89943955ead978dba068c9fb8661941)

## [0.1.2-rc.7] - 2025-08-27

### Changed

- Centralize MSBuild config via Directory.Build.props; drop MS_ARGS - [a70ce82](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=a70ce827239c19398e73700c7ce6135225e6a4e5)
- Enable CS0618 and IDE0251 warnings as errors for using obsolete APIs and readonly struct modifiers - [f9d7e39](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=f9d7e39e4bfedde1ff473f10fb57ffc986142084)
- Update `vnlib.core` to v0.1.2-rc.9 - (deps) [f8c6649](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=f8c6649236679ecdd3a434160fbde2c6d3c9f5e7)
- Adding more documentation to config substitution for mvc static route attributes - (loading) [14ad900](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=14ad900e6d2cd74baaa15642093be69d45f748f5)

### Fixed

- Add loading extensions unit testing project - [0a2051b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=0a2051bb3df2d9883e2854ec5043ba7ac72f74ae)

## [0.1.2-rc.6] - 2025-08-13

### Changed

- Patch for vnlib.core breaking change for IUmanagedHeap - [984590b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=984590b0e333e7878e04fb4148975b941ac42995)
- Update `Microsoft.EntityFrameworkCore` to v8.0.19 - (deps) [9c8c72c](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=9c8c72c954b2f7a45b759898b8ce2340684ea170)
- Update `EntityFrameworkCore.Sqlite` to v8.0.19 - (deps) [adc6472](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=adc6472a8c140a75d1632bd116bb6a22dfd27f19)
- Update vnlib.core to v0.1.2-rc.8 - (deps) [963e839](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=963e83946bef40acb3caa93dedc2b9a582e5890b)

### Fixed

- Fix dependency versions in nuget packages - [b339e60](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=b339e6006fe8a68a12a9f8cdb0fdbe2cdb8bee39)

## [0.1.2-rc.5] - 2025-07-24

### Changed

- Update ErrorProne.NET.CoreAnalyzers to 0.8.0 - (deps) [eae29c3](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=eae29c379726ff1732bc80ee01879cc872d3fdf5)
- Update Microsoft.EntityFrameworkCore to 8.0.18 - (deps) [7e138ac](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=7e138ac255e28435ff72d86aee6098a29b800de5)
- Update EntityFrameworkCore.Sqlite to 8.0.18 - (deps) [d8a9a1e](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=d8a9a1e0a6e8a09fbd245c0f230d8955f50ea185)
- Update Microsoft.EntityFrameworkCore.SqlServer to v8.0.18 - (deps) [1530586](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=153058682852dc4eecb48383ff3be93659d3e6cc)
- Update VNLIb.Core build version to v0.1.2-rc.7 - (deps) [10754fe](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=10754fe30153b411f8674776fa568466d872ce8f)

## [0.1.2-rc.4] - 2025-07-07

### Changed

- Pin core version to v0.1.2-rc.5 - (deps) [65155d6](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=65155d6fe4bd0ec1075c14c758e05c46f3ae6071)
- Bump vnlib.core version to v0.1.2-rc.6 - (deps) [27fbbdc](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=27fbbdc31dfadd977882bd8d6a421ff556b2a8c0)

## [0.1.2-rc.2] - 2025-06-13

### Added

- Add tagging task for current commit version - (ci) [5994a2b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=5994a2b28e3ee9c8962a440d247e0538be6cca9d)

## [0.1.1] - 2025-05-15

### Added

- Update modern SQLServer, add some DBBuilder extensions - [40c634b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=40c634b0f37ce9922dbc32c86e26d5a771daeca3)
- #2 Middleware helpers, proj cleanup, fix sync secrets, vault client - [8e77289](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=8e77289041349b16536497f48f0c0a4ec6fe30f5)
- Vault environment vars - [27fb538](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=27fb5382d80d9bcfb4c65974bbae20c5e7b8ccbc)
- Allow S3Config type inheritence - [bcbe51b](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=bcbe51bef546458cb7fee0d8f1dfd00cf936545a)
- Stage some mvc stuff - [1229ed7](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=1229ed75549de1c56aaee42c921acbd96c4d4c9b)
- Smiplify configuration helpers - [c8567e5](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=c8567e58dc1d4135da1f6cefa6fa66af5fcd7b19)
- Some mvc static routing extensions - [711b12f](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=711b12fa249cba9effecd4e722dd8d460d083659)
- Allow users to load custom vault providers - [4aa3494](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=4aa34942d031221b9177ccf0402c2ec33a551301)

### Changed

- Changed how service constructors are invoked, moved routing - [766e179](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=766e179d110db4f955fffce55f2b0ad41c139179)
- Overhaul secret loading. Remove VaultSharp as a dep - [7a263bf](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=7a263bf54b7967ddeb9f6b662339ec1c74546ce8)

### Fixed

- #3 Error raised when managed password type disposed - [21c6c85](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=21c6c85f540740ac29536a7091346a731aa85148)
- #3 Defer vault loading until a secret actually needs it - [69f13e4](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=69f13e43dfdd8069459800ccc3039f45fc884814)
- Endpoint initialization - [3f6a803](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=3f6a80306935afbd9cc74bd2bec83977a2ae12ae)
- Fix unexpected raw secret erasure - [4237750](https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/commit/?id=42377501eb066f99c8e9d3f4a89b7595329e519b)

[0.1.5]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.5&id2=v0.1.4
[0.1.4]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.4&id2=v0.1.3
[0.1.3]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.3&id2=v0.1.2
[0.1.2]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2&id2=v0.1.2-rc.8
[0.1.2-rc.8]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.8&id2=v0.1.2-rc.7
[0.1.2-rc.7]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.7&id2=v0.1.2-rc.6
[0.1.2-rc.6]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.6&id2=v0.1.2-rc.5
[0.1.2-rc.5]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.5&id2=v0.1.2-rc.4
[0.1.2-rc.4]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.4&id2=v0.1.2-rc.3
[0.1.2-rc.2]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.2&id2=v0.1.1

<!-- generated by git-cliff -->
