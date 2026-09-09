namespace Dapsman.Application;

public static class Utils
{	
	// Source - https://stackoverflow.com/a/64038750
	// Posted by bytedev, modified by community. See post 'Timeline' for change history
	// Retrieved 2026-08-28, License - CC BY-SA 4.0
	public static bool IsBinary(string filePath, int requiredConsecutiveNul = 1)
	{
		const int charsToCheck = 8000;
		const char nulChar = '\0';

		int nulCount = 0;

		using (var streamReader = new StreamReader(filePath))
		{
			for (var i = 0; i < charsToCheck; i++)
			{
				if (streamReader.EndOfStream)
					return false;

				if ((char) streamReader.Read() == nulChar)
				{
					nulCount++;

					if (nulCount >= requiredConsecutiveNul)
						return true;
				}
				else
				{
					nulCount = 0;
				}
			}
		}

		return false;
	}
}