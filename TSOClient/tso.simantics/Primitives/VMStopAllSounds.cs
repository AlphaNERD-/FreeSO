﻿using FSO.SimAntics.Engine;
using FSO.Files.Utils;
using System.IO;

namespace FSO.SimAntics.Primitives
{
    public class VMStopAllSounds : VMPrimitiveHandler
    {
        public override VMPrimitiveExitCode Execute(VMStackFrame context, VMPrimitiveOperand args)
        {
            var operand = (VMStopAllSoundsOperand)args;

            var owner = (operand.Flags == 1)?context.StackObject:context.Caller;
            if (owner == null) return VMPrimitiveExitCode.GOTO_TRUE;
            var threads = owner.SoundThreads;

            for (int i = 0; i < threads.Count; i++)
            {
                threads[i].Sound.RemoveOwner(owner.ObjectID);
            }
            threads.Clear();
            context.VM.SoundEntities.Remove(owner);

            return VMPrimitiveExitCode.GOTO_TRUE;
        }
    }

    public class VMStopAllSoundsOperand : VMPrimitiveOperand
    {
        public byte Flags;
        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes)
        {
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN))
            {
                Flags = io.ReadByte();
            }
        }
        public void Write(byte[] bytes) {
            using (var io = new BinaryWriter(new MemoryStream(bytes)))
            {
                io.Write(Flags);
            }
        }
        #endregion
    }
}
