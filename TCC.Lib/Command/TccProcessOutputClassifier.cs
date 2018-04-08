using System.Collections.Generic;

namespace TCC.Lib.Command
{
    public class TccProcessOutputClassifier : IProcessOutputClassifier
    {
        public static TccProcessOutputClassifier Instance { get; } = new TccProcessOutputClassifier();

        /// <summary>
        /// Filter "good" stderror as infos
        /// </summary>
        /// <returns></returns>
        public bool IsInfo(string line)
        {
            if (line.StartsWith("Compressed"))
            {
                return true;
            }
            return false;
        }

        public bool IsIgnorable(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return true;
            }

            if (line.StartsWith("*** LZ4 command line interface"))
            {
                return true;
            }
            if (_opensslInfos.Contains(line))
            {
                return true;
            }
            return false;
        }

        private static readonly HashSet<string> _opensslInfos = new HashSet<string>()
        {
            "options are",
            "-in <file>     input file",
            "-out <file>    output file",
            "-pass <arg>    pass phrase source",
            "-e             encrypt",
            "-d             decrypt",
            "-a/-base64     base64 encode/decode, depending on encryption flag",
            "-k             passphrase is the next argument",
            "-kfile         passphrase is the first line of the file argument",
            "-md            the next argument is the md to use to create a key",
            "                 from a passphrase.  One of md2, md5, sha or sha1",
            "-S             salt in hex is the next argument",
            "-K/-iv         key/iv in hex is the next argument",
            "-[pP]          print the iv/key (then exit if -P)",
            "-bufsize <n>   buffer size",
            "-nopad         disable standard block padding",
            "-engine e      use engine e, possibly a hardware device.",
            "Cipher Types",
            "-aes-128-cbc               -aes-128-ccm               -aes-128-cfb              ",
            "-aes-128-cfb1              -aes-128-cfb8              -aes-128-ctr              ",
            "-aes-128-ecb               -aes-128-ofb               -aes-192-cbc              ",
            "-aes-192-ccm               -aes-192-cfb               -aes-192-cfb1             ",
            "-aes-192-cfb8              -aes-192-ctr               -aes-192-ecb              ",
            "-aes-192-ofb               -aes-256-cbc               -aes-256-ccm              ",
            "-aes-256-cfb               -aes-256-cfb1              -aes-256-cfb8             ",
            "-aes-256-ctr               -aes-256-ecb               -aes-256-ofb              ",
            "-aes128                    -aes192                    -aes256                   ",
            "-bf                        -bf-cbc                    -bf-cfb                   ",
            "-bf-ecb                    -bf-ofb                    -blowfish                 ",
            "-camellia-128-cbc          -camellia-128-cfb          -camellia-128-cfb1        ",
            "-camellia-128-cfb8         -camellia-128-ecb          -camellia-128-ofb         ",
            "-camellia-192-cbc          -camellia-192-cfb          -camellia-192-cfb1        ",
            "-camellia-192-cfb8         -camellia-192-ecb          -camellia-192-ofb         ",
            "-camellia-256-cbc          -camellia-256-cfb          -camellia-256-cfb1        ",
            "-camellia-256-cfb8         -camellia-256-ecb          -camellia-256-ofb         ",
            "-camellia128               -camellia192               -camellia256              ",
            "-cast                      -cast-cbc                  -cast5-cbc                ",
            "-cast5-cfb                 -cast5-ecb                 -cast5-ofb                ",
            "-des                       -des-cbc                   -des-cfb                  ",
            "-des-cfb1                  -des-cfb8                  -des-ecb                  ",
            "-des-ede                   -des-ede-cbc               -des-ede-cfb              ",
            "-des-ede-ofb               -des-ede3                  -des-ede3-cbc             ",
            "-des-ede3-cfb              -des-ede3-cfb1             -des-ede3-cfb8            ",
            "-des-ede3-ofb              -des-ofb                   -des3                     ",
            "-desx                      -desx-cbc                  -id-aes128-CCM            ",
            "-id-aes128-wrap            -id-aes192-CCM             -id-aes192-wrap           ",
            "-id-aes256-CCM             -id-aes256-wrap            -id-smime-alg-CMS3DESwrap ",
            "-rc2                       -rc2-40-cbc                -rc2-64-cbc               ",
            "-rc2-cbc                   -rc2-cfb                   -rc2-ecb                  ",
            "-rc2-ofb                   -rc4                       -rc4-40                   ",
            "-seed                      -seed-cbc                  -seed-cfb                 ",
            "-seed-ecb                  -seed-ofb                  "
        };

    }
}