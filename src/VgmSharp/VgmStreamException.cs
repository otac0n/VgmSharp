// Copyright © John Gietzen. All Rights Reserved. This source is subject to the ISC license. Please see license.md for more information.

namespace VgmSharp
{
    public sealed class VgmStreamException : Exception
    {
        public VgmStreamException(string message) : base(message) { }

        public VgmStreamException(string message, Exception inner) : base(message, inner) { }
    }
}
