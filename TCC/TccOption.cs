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
		[Value(0, Required = true, HelpText = "Input files / folders to be processed.")]
		public string SourceDirOrFile { get; set; }

		[Option('o', "output", Required = true, HelpText = "Output directory")]
		public string DestinationDir { get; set; }

		public PasswordMode PasswordMode { get; set; }

		[Option('p', "pass", Required = false, SetName = "passwd", HelpText = "EncryptionPassword")]
		public string Password { get; set; }

		[Option('e', "passFile", Required = false, SetName = "passwd", HelpText = "Encryption Password in file")]
		public string PasswordFile { get; set; }

		[Option('k', "publicKey", Required = false, SetName = "passwd", HelpText = "Public encryption key")]
		public string PublicKey { get; set; }

		[Option('t', "thread", Required = false, HelpText = "Number of threads")]
		public string Threads { get; set; }

		[Option('f', "failfast", Required = false, HelpText = "Fail fast toggle")]
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