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

Basically, TCC job's is to pipe ( `|` ) tar, compressor and openssl commands. Natives and official implementations of each command are used for maximum performance.

The other TCC purpose is to prepare the compression job with differents strategies : for example, you can create an archive for each folder found in the source folder, and choose how many parallel threads to process the batch for maximum throughput. You can either saturate disk ios, or the CPU depending on your settings, or choose to keep some room on your servers.

TCC is actually used for off-site backup operations on production servers.

## How to install : 

- Install the [.NET Core 2.1 Runtime](https://www.microsoft.com/net/download)
- Install TCC as global tool :
    ```dotnetcli
    dotnet tool install -g TCC
    ```

- Run TCC in command line :
    ```dotnetcli
    tcc --help
    ```

- Don't hesitate to use the benchmark mode on your data to find the better speed / compression tradeoff in your case : 
    ```dotnetcli
    tcc benchmark C:\ToBackupFolder
    ```

## Recommandations : 

For maximum performances, you have to backup files from one physical disk, and output archives on another physical disk. IOps are the main bottleneck even on a recent SSD.

## Current status : 
- alpha : use with care, API and archive format are subject to breaking changes. Be sure to keep the version you use actually in order to be able to decrypt your archives. 

## Roadmap :
- [ ] documenatation
- [x] operation logs
- [ ] differential backup
- [x] password file / asymetric key support
- [ ] external storage provider support
- [x] exe packaging + distribution
- [x] lz4 / brotli / zstd support
- [x] benchmark mode

## Plateform support : 
- [x] Windows
- [ ] Linux

## Dependencies : 

This project relies on the following external dependencies :

On windows :
- Tar v1.30 : extracted from the msys2 build of [git for windows 2.19.1](https://git-scm.com/)
- OpenSsl v1.1.1 : from [bintray](https://bintray.com/vszakats/generic/openssl) referenced in [official wiki](https://wiki.openssl.org/index.php/Binaries)
- Zstandard v1.3.5 : from facebook [Zstandard github repo](https://github.com/facebook/zstd/)
- Brotli v1.0.4 : from google [Brotli github repo](https://github.com/google/brotli/)
- Lz4 v1.8.3 : from [Lz4 github repo](https://github.com/lz4/lz4/)

All dependencies are downloaded on first TCC start, and are not included in TCC repository, except tar (for now)
Please consult the different project licences.

TCC is inspired from the excellent Squash Compression Benchmark : https://quixdb.github.io/squash-benchmark/