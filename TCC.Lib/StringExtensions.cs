namespace TCC.Lib
{
	public static class StringExtensions
	{
		public static string Escape(this string str)
		{
			return '"' + str.Trim('"') + '"';
		}

	}
}