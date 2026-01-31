# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
