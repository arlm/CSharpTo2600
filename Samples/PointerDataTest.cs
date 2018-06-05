using VCSFramework;
using static VCSFramework.Registers;

namespace Samples
{
	public unsafe static class PointerDataTest
    {
		[RomData(0xAB, 0xBC, 0xCD, 0xDE, 0xEF)]
		public static byte* Data;

		public static void Main()
		{
			ColuBk = Data[0];
		}
    }
}
