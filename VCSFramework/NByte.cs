﻿using System;

namespace VCSFramework
{
	[DoNotCompile]
    public class NByte
    {
		private byte Value;

		[CompilerImplemented]
		public static implicit operator NByte(int @int) => new NByte((byte)@int);

		public NByte(byte value) => Value = value;
    }
}
