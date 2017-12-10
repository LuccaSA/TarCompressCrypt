using CommandLine;

namespace TCC
{
	[Verb("compress", HelpText = "Create archive from file/folders")]
	public class CompressOption : TccOption
	{
		[Option('i', "individual", Required = false, HelpText = "Create a distinc archive foreach file / folder", Default = true)]
		public bool Individual { get; set; }
	}

	[Verb("decompress", HelpText = "Extract file/folders from archive")]
	public class DecompressOption : TccOption
	{


	}

	public class TccOption
	{
		[Option(Required = true, HelpText = "Input files / folders to be processed.")]
		public string SourceDirOrFile { get; set; }

		[Option('o', "output", Required = true, HelpText = "Output directory")]
		public string DestinationDir { get; set; }

		public PasswordMode PasswordMode { get; set; }

		[Option('p', "pass", Required = true, HelpText = "EncryptionPassword")]
		public string Password { get; set; }

		[Option('e', "passFile", Required = true, HelpText = "Encryption Password in file")]
		public string PasswordFile { get; set; }

		[Option('k', "publicKey", Required = true, HelpText = "Public encryption key")]
		public string PublicKey { get; set; }

		[Option('t', "thread", Required = true, HelpText = "Number of threads")]
		public string Threads { get; set; }

		[Option('f', "failfast", Required = true, HelpText = "Fail fast toggle")]
		public bool FailFast { get; set; }

		// source dirextory or source file
		// -c => compress
		// -d => decompress

		// -i => individual mode
		// -p password
		// -pf password file
		// -pk public key
		// -t threads
		// -out destination
		// -failfast
	}
}