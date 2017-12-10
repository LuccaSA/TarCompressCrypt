namespace TCC
{
	public class CommandResult
	{
		public int ExitCode { get; set; }
		public bool IsSuccess { get; set; }
		public string Output { get; set; }
		public string Errors { get; set; }

		public string Command { get; set; }
		public Block Block { get; set; }
	}
}