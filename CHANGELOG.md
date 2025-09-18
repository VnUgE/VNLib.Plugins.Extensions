# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[0.1.2-rc.8]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.8&id2=v0.1.2-rc.7
[0.1.2-rc.7]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.7&id2=v0.1.2-rc.6
[0.1.2-rc.6]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.6&id2=v0.1.2-rc.5
[0.1.2-rc.5]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.5&id2=v0.1.2-rc.4
[0.1.2-rc.4]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.4&id2=v0.1.2-rc.3
[0.1.2-rc.2]: https://git.vaughnnugent.com/cgit/vnuge/vnlib-plugins-extensions.git/diff?id=v0.1.2-rc.2&id2=v0.1.1

<!-- generated by git-cliff -->
