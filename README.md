# TarCompressCrypt

[![Build status](https://ci.appveyor.com/api/projects/status/9mdd3hlgm234ey38/branch/master?svg=true)](https://ci.appveyor.com/project/Raph/tarcompresscrypt/branch/master)
[![codecov](https://codecov.io/gh/rducom/TarCompressCrypt/branch/master/graph/badge.svg)](https://codecov.io/gh/rducom/TarCompressCrypt)
[![Sonarcloud coverage](https://sonarcloud.io/api/project_badges/measure?project=TCC&metric=coverage)](https://sonarcloud.io/dashboard?id=TCC)
[![Sonarcloud Status](https://sonarcloud.io/api/project_badges/measure?project=TCC&metric=alert_status)](https://sonarcloud.io/dashboard?id=TCC)
[![Sonarcloud Debt](https://sonarcloud.io/api/project_badges/measure?project=TCC&metric=sqale_index)](https://sonarcloud.io/dashboard?id=TCC)
[![Sonarcloud Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=TCC&metric=vulnerabilities)](https://sonarcloud.io/dashboard?id=TCC)

TarCompressCrypt (TCC) is a command line tool for blazing fast compression + encryption / decompression + decryption
- package multiple files / folders with tar
- then use lz4/zstd/brotli for blazing fast compression operations
- then use openssl for aes256 encryption (native aes-ni instructions support)

All operations are parallelized for maximum throughput. The goal is to saturate disk ios, not the CPU

Tcc is mainly used for massive backup operation on production servers, with minimum CPU impact

The bottleneck will be your storage :

![image](https://user-images.githubusercontent.com/5228175/33807616-e57be752-ddd9-11e7-8ea8-0b26cae6e228.png)

(on 16 vcore Ryzen 1700 no oc & 960 pro SSD)

### How to install : 

- Install the [.NET Core 2.1 Runtime](https://www.microsoft.com/net/download)
- Install TCC as global tool :
```
dotnet tool install -g TCC
```
- Run TCC in command line :
```
tcc --help
```

Don't hesitate to use the benchmark mode on your data to find the better speed / compression tradeoff in your case : 
```
tcc benchmark C:\ToBackupFolder
```

### Recommandations : 

For maximum performances, you have to backup files from one physical disk, and output archives on another physical disk. IOps are the main bottleneck even on a recent SSD.

### Current status : 
- alpha : use with care, API and archive format are subject to breaking changes. Be sure to keep the version you use actually in order to be able to decrypt your archives. 

### Roadmap :
- [ ] documenatation
- [x] operation logs
- [ ] differential backup
- [x] password file / asymetric key support
- [ ] external storage provider support
- [x] exe packaging + distribution
- [x] lz4 / brotli / zstd support
- [x] benchmark mode

### Plateform support : 
- [x] Windows
- [ ] Linux

This project is inspired from the excellent Squash Compression Benchmark : https://quixdb.github.io/squash-benchmark/