/*
	Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/
namespace Alphora.Dataphor.DAE.Streams
{
    using System;
    using System.IO;
    using System.Collections;
    using System.Runtime.Serialization.Formatters.Binary;

    using Alphora.Dataphor;
	using Alphora.Dataphor.BOP;
	using Alphora.Dataphor.DAE.Runtime.Data;
	
	// TODO: Investigate the usage of BitConverter instead of unsafe fixes

	/// <remarks>    
    /// A Conveyor provides host language access to the physical representation of Dataphor values stored in streams
    /// All Conveyors must descend from this base and provide a single constructor which takes a Stream as its only parameter
    /// All reading and writing of Dataphor values in the host language should be done through these Conveyors, 
    /// although it is not strictly necessary to do so (physical access could occur in instruction implementations, but is not recommended)
    /// The intent of the Conveyor is to provide a single access point for host language access to Dataphor values.
    /// </remarks>
    public abstract class Conveyor : Object //, IConveyor
    {
		public Conveyor() {}

		/// <summary>Indicates whether this conveyor uses the streaming read/write methods, or the byte[] read/write and GetSize methods.</summary>
		public virtual bool IsStreaming { get { return false; } }

		/// <summary>Returns the size in bytes required to store the given value.</summary>		
		public virtual int GetSize(object AValue)
		{
			throw new NotSupportedException();
		}

		/// <summary>Returns the native representation of the value stored in the buffer given by ABuffer, beginning at the offset given by AOffset.</summary>
		public virtual object Read(byte[] ABuffer, int AOffset)
		{
			throw new NotSupportedException();
		}
		
		/// <summary>Writes the physical representation of AValue into the buffer given by ABuffer beginning at the offset given by AOffset</summary>		
		public virtual void Write(object AValue, byte[] ABuffer, int AOffset)
		{
			throw new NotSupportedException();
		}

		/// <summary>Returns the native representation of the value stored in the stream given by AStream.</summary>
		public virtual object Read(Stream AStream)
		{
			throw new NotSupportedException();
		}
		
		/// <summary>Writes the physical representation of the value given by AValue into the stream given by AStream.</summary>
		public virtual void Write(object AValue, Stream AStream)
		{
			throw new NotSupportedException();
		}
    }
    
    public class BooleanConveyor : Conveyor
    {
		public BooleanConveyor() : base() {}
		
		public unsafe override int GetSize(object AValue)
		{
			return (sizeof(bool));
		}

		public override object Read(byte[] ABuffer, int AOffset)
		{
			return ABuffer[AOffset] != 0;
		}

		public override void Write(object AValue, byte[] ABuffer, int AOffset)
		{
			ABuffer[AOffset] = (byte)((bool)AValue ? 1 : 0);
		}
    }
    
    public class ByteConveyor : Conveyor
    {
		public ByteConveyor() : base() {}
		
		public unsafe override int GetSize(object AValue)
		{
			return (sizeof(byte));
		}

		public override object Read(byte[] ABuffer, int AOffset)
		{
			return ABuffer[AOffset];
		}

		public override void Write(object AValue, byte[] ABuffer, int AOffset)
		{
			ABuffer[AOffset] = (byte)AValue;
		}
    }
    
    public class Int16Conveyor : Conveyor
    {
		public Int16Conveyor() : base() {}
		
		public unsafe override int GetSize(object AValue)
		{
			return (sizeof(short));
		}

		public unsafe override object Read(byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				return *((short*)LBufferPtr);
			}
		}

		public unsafe override void Write(object AValue, byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				*((short*)LBufferPtr) = (short)AValue;
			}
		}
    }
    
    public class Int32Conveyor : Conveyor
    {
		public Int32Conveyor() : base() {}
		
		public unsafe override int GetSize(object AValue)
		{
			return (sizeof(int));
		}

		public unsafe override object Read(byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				return *((int*)LBufferPtr);
			}
		}

		public unsafe override void Write(object AValue, byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				*((int*)LBufferPtr) = (int)AValue;
			}
		}
    }
    	
    public class Int64Conveyor : Conveyor
    {
		public Int64Conveyor() : base() {}
		
		public unsafe override int GetSize(object AValue)
		{
			return (sizeof(long));
		}

		public unsafe override object Read(byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				return *((long*)LBufferPtr);
			}
		}

		public unsafe override void Write(object AValue, byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				*((long*)LBufferPtr) = (long)AValue;
			}
		}
    }
    
    public class DecimalConveyor : Conveyor
    {
		public DecimalConveyor() : base() {}
		
		public unsafe override int GetSize(object AValue)
		{
			return (sizeof(decimal));
		}

		public unsafe override object Read(byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				return *((decimal*)LBufferPtr);
			}
		}

		public unsafe override void Write(object AValue, byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				*((decimal*)LBufferPtr) = (decimal)AValue;
			}
		}
    }
    
    public class StringConveyor : Conveyor
    {
		public StringConveyor() : base() {}
		
		public unsafe override int GetSize(object AValue)
		{
			return sizeof(int) + (((string)AValue).Length * 2); 
		}

		public unsafe override object Read(byte[] ABuffer, int AOffset)
		{
			int LLength = 0;
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				LLength = *((int*)LBufferPtr);				
			}
			
			if (LLength > 0)
			{
				fixed (byte* LBufferPtr = &(ABuffer[AOffset + sizeof(int)]))
				{
					return new String((char*)LBufferPtr, 0, LLength);
				}
			}

			return String.Empty;
		}

		public unsafe override void Write(object AValue, byte[] ABuffer, int AOffset)
		{
			string LString = (String)AValue;
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				*((int*)LBufferPtr) = LString.Length;
			}
			
			if (LString.Length > 0)
			{
				fixed (byte* LBufferPtr = &(ABuffer[AOffset + sizeof(int)]))
				{
					char* LCurrentPtr = (char*)LBufferPtr;
					for (int LIndex = 0; LIndex < LString.Length; LIndex++)
					{
						*LCurrentPtr = LString[LIndex];
						LCurrentPtr++;
					}
				}
			}
		}
    }
    
    public class DateTimeConveyor : Conveyor
    {
		public DateTimeConveyor() : base() {}
		
		public unsafe override int GetSize(object AValue)
		{
			return (sizeof(long));
		}

		public unsafe override object Read(byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				return new DateTime(*((long*)LBufferPtr));
			}
		}

		public unsafe override void Write(object AValue, byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				*((long*)LBufferPtr) = ((DateTime)AValue).Ticks;
			}
		}
    }
    
    public class TimeSpanConveyor : Conveyor
    {
		public TimeSpanConveyor() : base() {}
		
		public unsafe override int GetSize(object AValue)
		{
			return sizeof(long);
		}

		public unsafe override object Read(byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				return new TimeSpan(*((long*)LBufferPtr));
			}
		}

		public unsafe override void Write(object AValue, byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				*((long*)LBufferPtr) = ((TimeSpan)AValue).Ticks;
			}
		}
    }
    
    public class GuidConveyor : Conveyor
    {
		public GuidConveyor() : base() {}
		
		public unsafe override int GetSize(object AValue)
		{
			return (sizeof(Guid));
		}

		public unsafe override object Read(byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				return *((Guid*)LBufferPtr);
			}
		}

		public unsafe override void Write(object AValue, byte[] ABuffer, int AOffset)
		{
			fixed (byte* LBufferPtr = &(ABuffer[AOffset]))
			{
				*((Guid*)LBufferPtr) = (Guid)AValue;
			}
		}
    }
    
	public class ObjectConveyor : Conveyor
	{
		public ObjectConveyor() : base() {}
		
		public override bool IsStreaming { get { return true; } }
		
		private BinaryFormatter FSerializer = new BinaryFormatter();
		
		public override object Read(Stream AStream)
		{
			return FSerializer.Deserialize(AStream);
		}
		
		public override void Write(object AValue, Stream AStream)
		{
			FSerializer.Serialize(AStream, AValue);
		}
	}

	public class BOPObjectConveyor : Conveyor
	{
		public BOPObjectConveyor() : base() {}
		
		public override bool IsStreaming { get { return base.IsStreaming; } }
		
		private Serializer FSerializer = new Serializer();
		private Deserializer FDeserializer = new Deserializer();
		
		public override object Read(Stream AStream)
		{
			return FDeserializer.Deserialize(AStream, null);
		}
		
		public override void Write(object AValue, Stream AStream)
		{
			FSerializer.Serialize(AStream, AValue);
		}
	}

	public class BinaryConveyor : Conveyor
	{
		public override bool IsStreaming { get { return true; } }
		
		private BinaryFormatter FSerializer = new BinaryFormatter();
		
		public override object Read(Stream AStream)
		{
			byte[] LData = new byte[AStream.Length];
			AStream.Read(LData, 0, (int)AStream.Length);
			return LData;
		}
		
		public override void Write(object AValue, Stream AStream)
		{
			byte[] LValue = (byte[])AValue;
			AStream.Write(LValue, 0, LValue.Length);
		}
	}
}
