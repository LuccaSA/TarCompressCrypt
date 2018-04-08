# TarCompressCrypt

[![Build status](https://ci.appveyor.com/api/projects/status/9mdd3hlgm234ey38/branch/master?svg=true)](https://ci.appveyor.com/project/Raph/tarcompresscrypt/branch/master)
[![codecov](https://codecov.io/gh/rducom/TarCompressCrypt/branch/master/graph/badge.svg)](https://codecov.io/gh/rducom/TarCompressCrypt)
[![Quality Gate](https://sonarcloud.io/api/badges/gate?key=TCC)](https://sonarcloud.io/dashboard?id=TCC)


TarCompressCrypt is a command line tool for blazing fast compression + encryption / decompression + decryption
- package multiple files / folders with tar
- use lz4 for blazing fast compression operations
- use openssl for aes256 encryption (native aes-ni instructions support)

It's aimed for fast backup operation on production server, with minimum CPU impact

The bottleneck will be your storage :

![image](https://user-images.githubusercontent.com/5228175/33807616-e57be752-ddd9-11e7-8ea8-0b26cae6e228.png)

(on 16 vcore Ryzen 1700 no oc & 960 pro SSD)

Why LZ4 ? : https://quixdb.github.io/squash-benchmark/

### Current status : 
- alpha : DOT NOT USE IN PRODUCTION  

### Roadmap :
- [ ] documenatation
- [ ] operation logs
- [ ] differential backup
- [x] password file / asymetric key support
- [ ] external storage provider support
- [x] exe packaging + distribution
- [ ] brotli support
- [ ] benchmark mode

### Plateform support : 
- [x] Windows
- [ ] Linux
