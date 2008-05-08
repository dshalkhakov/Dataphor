/*
	Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/
#define WRAPRUNTIMEEXCEPTIONS

using System;
using System.Text;
using System.Collections;
using System.Reflection;
using System.Reflection.Emit;

using Alphora.Dataphor.DAE.Server;	
using Alphora.Dataphor.DAE.Language;
using Alphora.Dataphor.DAE.Language.D4;

namespace Alphora.Dataphor.DAE.Runtime.Instructions
{
	/*
		DAE Processor Calling Convention ->
		
			The calling convention is distributed among several diferrent nodes, PlanNode, InstructionNode, CallNode, AggregateCallNode, and TableNode.
			
			PlanNode is the base class for all nodes in the processor, and introduces the methods used in the execution.
				
				DataVar Execute(ServerProcess AProcess)
					This method passes execution to the device if it is supported.
					Otherwise, it passes execution to the InternalExecute(ServerProcess) method.
				
				abstract DataVar InternalExecute(ServerProcess AProcess)
				
				virtual DataVar InternalExecute(ServerProcess AProcess, DataVar[] AArguments)
					This method raises on PlanNode, and is overridden by Instructions to perform their operations.
				
			InstructionNode is the base class for all instructions in the processor, and performs argument evaluation and virtual resolution.
			
				override DataVar InternalExecute(ServerProcess AProcess)
					Builds the arguments array based on the parameters in Operator, and the child nodes of the node.
					Performs virtual resolution, if necessary
					Calls InternalExecute(ServerProcess, DataVar[])
					Cleans up the arguments list
					
			CallNode is the class used to invoke D4 compiled operators, and prepares the stack prior to the call, and cleans it up afterwards.
			
				override DataVar InternalExecute(ServerProcess AProcess, DataVar[] AArguments)
					Pushes the arguments onto the stack, and performs a Call on the stack
					Calls Execute on the compiled BlockNode for Operator
					Pops the arguments off the stack and performs a return, pushing the result if necessary
					
			AggregateCallNode is used to invoke D4 aggregate operators.
			
			TableNode is the base class for all host implemented table instructions.
					
	*/
	
	public delegate DataVar ExecuteDelegate(ServerProcess AProcess);
        
	/// <summary> PlanNode </summary>	
	public abstract class PlanNode : System.Object
	{
		public PlanNode() : base()
		{
			Execute = new ExecuteDelegate(StandardExecute);
 		}
		
        // IsLiteral
        protected bool FIsLiteral = true;
        public bool IsLiteral
        {
			get { return FIsLiteral; }
			set { FIsLiteral = value; }
        }
        
        // IsFunctional
        protected bool FIsFunctional = true;
        public bool IsFunctional
        {
			get { return FIsFunctional; }
			set { FIsFunctional = value; }
        }
        
        // IsDeterministic
        protected bool FIsDeterministic = true;
        public bool IsDeterministic
        {
			get { return FIsDeterministic; }
			set { FIsDeterministic = value; }
        }
        
        // IsRepeatable
        protected bool FIsRepeatable = true;
        public bool IsRepeatable
        {
			get { return FIsRepeatable; }
			set { FIsRepeatable = value; }
        }

        // IsNilable
        protected bool FIsNilable = false;
        public bool IsNilable
        {
			get { return FIsNilable; }
			set { FIsNilable = value; }
		}
        
		public static string CharacteristicsToString(PlanNode ANode)
		{
			StringBuilder LString = new StringBuilder();
			LString.Append(ANode.IsLiteral ? Strings.Get("Characteristics.Literal") : Strings.Get("Characteristics.NonLiteral"));
			LString.AppendFormat(", {0}", ANode.IsFunctional ? Strings.Get("Characteristics.Functional") : Strings.Get("Characteristics.NonFunctional"));
			LString.AppendFormat(", {0}", ANode.IsDeterministic ? Strings.Get("Characteristics.Deterministic") : Strings.Get("Characteristics.NonDeterministic"));
			LString.AppendFormat(", {0}", ANode.IsRepeatable ? Strings.Get("Characteristics.Repeatable") : Strings.Get("Characteristics.NonRepeatable"));
			LString.AppendFormat(", {0}", ANode.IsNilable ? Strings.Get("Characteristics.Nilable") : Strings.Get("Characteristics.NonNilable"));
			return LString.ToString();
		}

        // IsOrderPreserving (see the sargability discussion in RestrictNode.cs for a description of this characteristic)
        protected bool FIsOrderPreserving = false;
        public bool IsOrderPreserving
        {
			get { return FIsOrderPreserving; }
			set { FIsOrderPreserving = value; }
		}
		
		// IsContextLiteral (see the sargability discussion in RestrictNode.cs for a description of this characteristic)
		public virtual bool IsContextLiteral(int ALocation)
		{
			for (int LIndex = 0; LIndex < Nodes.Count; LIndex++)
				if (!Nodes[LIndex].IsContextLiteral(ALocation))
					return false;
			return true;
		}
		
		// Modifiers
		protected LanguageModifiers FModifiers;
		public LanguageModifiers Modifiers
		{
			get { return FModifiers; }
			set { FModifiers = value; }
		}
        
        // Device
        protected Schema.Device FDevice;
        public Schema.Device Device
        {
			get { return FDevice; }
			set { FDevice = value; }
        }
        
        // DeviceNode
        protected DevicePlanNode FDeviceNode;
        public DevicePlanNode DeviceNode
        {
			get { return FDeviceNode; }
			set { FDeviceNode = value; }
		}
        
        // NoDevice
        protected bool FNoDevice;
        public bool NoDevice
        {
			get { return FNoDevice; }
			set { FNoDevice = value; }
        }
        
        // DeviceSupported
        protected bool FDeviceSupported;
        public bool DeviceSupported
        {
			get { return FDeviceSupported; }
			set { FDeviceSupported = value; }
        }
        
        // DeviceMessages
        // Set by the device during the prepare phase.
        // Will be null if this node has not been prepared, so test all access to this reference.
        protected Schema.TranslationMessages FDeviceMessages;
        public Schema.TranslationMessages DeviceMessages { get { return FDeviceMessages; } }
        
		// DetermineDevice
		public virtual void DetermineDevice(Plan APlan)
		{
			//	if child nodes are flagged with nodevice, or have more than one non-null device
			//		this node is flagged as nodevice
			//	otherwise
			//		this node uses the same device as its children
            Schema.Device LChildDevice = null;
            Schema.Device LCurrentChildDevice = null;
            FNoDevice = !ShouldSupport;
            
            foreach (PlanNode LNode in Nodes)
            {
				FNoDevice = FNoDevice || LNode.NoDevice;
				if (!FNoDevice)
				{
					LChildDevice = LNode.Device;
					if (LChildDevice != null)
					{
						if (LCurrentChildDevice == null)
							LCurrentChildDevice = LChildDevice;
						else if (LCurrentChildDevice != LChildDevice)
						{
							#if TRACEEVENTS
							if ((DataType != null) && !IgnoreUnsupported && !APlan.InTypeOfContext)
								APlan.ServerProcess.ServerSession.Server.RaiseTraceEvent(APlan.ServerProcess, TraceCodes.UnsupportedNode, String.Format(@"Node ""{0}"" not supported because arguments have disparate device sources.", this.GetType().Name));
							#endif
							FNoDevice = true;
							break;
						}
					}
				}
            }

            if (!FNoDevice)
            {
				FDevice = LCurrentChildDevice;
				if (FDevice != null)
				{							
					Schema.DevicePlan LDevicePlan = null;
					if (ShouldSupport)
					{
						APlan.ServerProcess.EnsureDeviceStarted(FDevice);
						LDevicePlan = FDevice.Prepare(APlan, this);
					}
					
					if (LDevicePlan != null)
						FDeviceMessages = LDevicePlan.TranslationMessages;

					if ((LDevicePlan != null) && LDevicePlan.IsSupported)
					{
						// If the plan could be supported via parameterization, it is not actually supported by the device
						// and setting the device supported to false ensures that if this node is actually executed, the
						// device will not be asked to perform a useless parameterization.
						FDeviceSupported = !FCouldSupport;
						APlan.AddDevicePlan(LDevicePlan);
					}
					else
					{
						if (!FDevice.IgnoreUnsupported && (DataType != null) && !IgnoreUnsupported && !APlan.InTypeOfContext)
						{
							if ((LDevicePlan != null) && !APlan.ServerProcess.SuppressWarnings)
								APlan.Messages.Add(new CompilerException(CompilerException.Codes.UnsupportedPlan, CompilerErrorLevel.Warning, FDevice.Name, SafeEmitStatementAsString(), LDevicePlan.TranslationMessages.ToString()));
							#if TRACEEVENTS
							APlan.ServerProcess.ServerSession.Server.RaiseTraceEvent(APlan.ServerProcess, TraceCodes.UnsupportedNode, String.Format(@"Node ""{0}"" not supported by device ""{1}"".", this.GetType().Name, FDevice.Name));
							#endif
						}
						FDeviceSupported = false;
						FNoDevice = true;
					}
				}
				else
				{
					FDeviceSupported = false;
					// BTR 4/28/2005 ->
					// Not sure why this is here, but it is preventing the translation of several otherwise translatable items.
					// I will turn it off and see what happens.
					//if (DataType is Schema.ITableType)
					//	FNoDevice = true;
				}
			}
			else
				FDeviceSupported = false;
		}
		
		// IgnoreUnsupported -- only applies if DataType is not null
		private bool FIgnoreUnsupported;
		public bool IgnoreUnsupported
		{
			get { return FIgnoreUnsupported; }
			set { FIgnoreUnsupported = value; }
		}
		
		private bool FCouldSupport = false;
		/// <summary>Set by the device to indicate that the node could be supported if necessary, but only by parameterization.</summary>
		public bool CouldSupport
		{
			get { return FCouldSupport; }
			set { FCouldSupport = value; }
		}
		
		private bool FShouldSupport = true;
		public bool ShouldSupport
		{
			get { return FShouldSupport; }
			set { FShouldSupport = value; }
		}
		
		private bool FShouldEmitIL = false;
		public bool ShouldEmitIL
		{
			get { return FShouldEmitIL; }
			set { FShouldEmitIL = value; }
		}
		
		private int FLine = -1;
		public int Line
		{
			get { return FLine; }
			set { FLine = value; }
		}
		
		private int FLinePos = -1;
		public int LinePos
		{
			get { return FLinePos; }
			set { FLinePos = value; }
		}
		
        // Nodes
        public PlanNodes Nodes = new PlanNodes();

		// IL Generation        
        public void EmitIL(Plan APlan, bool AParentEmitted) 
        {
			if (!FDeviceSupported)
			{
				if (!AParentEmitted)
					InternalEmitIL(APlan);
				
				foreach (PlanNode LNode in Nodes)
					LNode.EmitIL(APlan, FShouldEmitIL);
			}
        }

		protected static FieldInfo FNodesFieldInfo = typeof(PlanNode).GetField("Nodes");
		protected static FieldInfo FPlanNodesFieldInfo = typeof(PlanNodes).GetField("FNodes", BindingFlags.NonPublic | BindingFlags.Instance);        
        protected static FieldInfo FExecuteFieldInfo = typeof(PlanNode).GetField("Execute");
        protected static MethodInfo FExecuteMethodInfo = typeof(ExecuteDelegate).GetMethod("Invoke", new Type[] { typeof(ServerProcess) });
        protected static MethodInfo FPlanNodesIndexerInfo = typeof(PlanNodes).GetMethod("get_Item", new Type[] { typeof(int) });

		/// <summary>
		/// Prepares a dynamic method for use in generating the IL for a node.
		/// </summary>
		/// <returns>The DynamicMethod instance representing the Execute method being constructed.</returns>
		protected DynamicMethod BeginEmitIL()
		{
			return new DynamicMethod(GetType().Name, typeof(DataVar), new Type[] { typeof(PlanNode), typeof(ServerProcess) }, GetType(), true);
		}

		/// <summary>
		/// If AExecute is not null, calls the CreateDelegate of the DynamicMethod and sets the execute field of the node to the value returned.
		/// </summary>
		/// <param name="AExecute">The DynamicMethod instance returned by a previous call to BeginEmitIL.</param>
		protected void EndEmitIL(DynamicMethod AExecute, ILGenerator AGenerator)
		{
			if (DataType == null)
				AGenerator.Emit(OpCodes.Ldnull);
			AGenerator.Emit(OpCodes.Ret);
			Execute = (ExecuteDelegate)AExecute.CreateDelegate(typeof(ExecuteDelegate), this);
		}
		
		public DataVar TestExecute(ServerProcess AProcess)
		{
			return Nodes[0].Execute(AProcess);
		}
		
		public LocalBuilder EmitThis(Plan APlan, ILGenerator AGenerator, int[] AExecutePath)
		{
			// Declare a local variable to store the "this" pointer for the method (This is a path to the PlanNode for this node from the root IL generation node)
			LocalBuilder LThis = AGenerator.DeclareLocal(typeof(PlanNode));
			
			// Load this of root plan node
			AGenerator.Emit(OpCodes.Ldarg_0);

			// for each index in the execute path, load the node reference in the Nodes list for that node
			for (int LIndex = 0; LIndex < AExecutePath.Length; LIndex++)
			{
			    AGenerator.Emit(OpCodes.Ldfld, FNodesFieldInfo);
				AGenerator.Emit(OpCodes.Ldfld, FPlanNodesFieldInfo);
				AGenerator.Emit(OpCodes.Ldc_I4, AExecutePath[LIndex]);
				AGenerator.Emit(OpCodes.Ldelem_Ref);
			}
			
			// Store it in the "this" local
			AGenerator.Emit(OpCodes.Stloc, LThis);

			return LThis;			
		}
		
		public int[] PrepareExecutePath(Plan APlan, int[] AExecutePath)
		{
			int[] LExecutePath = new int[AExecutePath.Length + 1];
			AExecutePath.CopyTo(LExecutePath, 0);
			return LExecutePath;
		}
		
		public void EmitExecute(Plan APlan, ILGenerator AGenerator, int[] AExecutePath, int ANodeIndex)
		{
			AExecutePath[AExecutePath.Length - 1] = ANodeIndex;
			Nodes[ANodeIndex].EmitIL(APlan, AGenerator, AExecutePath);
			
			// Pop the return value if necessary
			if (Nodes[ANodeIndex].DataType != null)
				AGenerator.Emit(OpCodes.Pop);
		}
		
		public void EmitEvaluate(Plan APlan, ILGenerator AGenerator, int[] AExecutePath, int ANodeIndex)
		{
			AExecutePath[AExecutePath.Length - 1] = ANodeIndex;
			Nodes[ANodeIndex].EmitIL(APlan, AGenerator, AExecutePath);
		}
		
		public virtual void EmitIL(Plan APlan, ILGenerator AGenerator, int[] AExecutePath)
		{
			// Generate a call to the Execute delegate for this node
			
			LocalBuilder LThis = EmitThis(APlan, AGenerator, AExecutePath);
			
			// Load the address of the execute delegate for the "this" node
			AGenerator.Emit(OpCodes.Ldloc, LThis);
			AGenerator.Emit(OpCodes.Ldfld, FExecuteFieldInfo);
			
			// Prepare the execute call arguments
			AGenerator.Emit(OpCodes.Ldarg_1);		 // AProcess
			
			// Invoke Execute
			AGenerator.EmitCall(OpCodes.Callvirt, FExecuteMethodInfo, null);
			
			// If no return value, eat the null
			if (DataType == null)
				AGenerator.Emit(OpCodes.Pop);
		}
		
		protected virtual void InternalEmitIL(Plan APlan) 
		{
			if (FShouldEmitIL)
			{
				DynamicMethod LExecute = BeginEmitIL();
				ILGenerator LGenerator = LExecute.GetILGenerator();
				EmitIL(APlan, LGenerator, new int[]{});
				EndEmitIL(LExecute, LGenerator);
			} 
		}
		
		// Statement
		public virtual Statement EmitStatement(EmitMode AMode)
		{
			throw new RuntimeException(RuntimeException.Codes.StatementNotSupported, ToString());
		}
		
		public string EmitStatementAsString(bool ARemoveLineBreaks)
		{
			string LStatement = new D4TextEmitter().Emit(EmitStatement(EmitMode.ForCopy));
			if (ARemoveLineBreaks)
			{
				bool LInWhiteSpace = false;
				StringBuilder LBuilder = new StringBuilder();
				for (int LIndex = 0; LIndex < LStatement.Length; LIndex++)
				{
					if (Char.IsWhiteSpace(LStatement, LIndex))
					{
						if (!LInWhiteSpace)
						{
							LInWhiteSpace = true;
							LBuilder.Append(" ");
						}
					}
					else
					{
						LInWhiteSpace = false;
						LBuilder.Append(LStatement[LIndex]);
					}
				}
				return LBuilder.ToString();
			}
			return LStatement;
		}
		
		public string EmitStatementAsString()
		{
			return EmitStatementAsString(true);
		}
		
		public string SafeEmitStatementAsString()
		{
			return SafeEmitStatementAsString(true);
		}
		
		public string SafeEmitStatementAsString(bool ARemoveLineBreaks)
		{
			try
			{
				return EmitStatementAsString(ARemoveLineBreaks);
			}
			catch
			{
				return String.Format(@"Statement cannot be emitted for plan nodes of type ""{0}"".", GetType().Name);
			}
		}

		// DataType
		protected Schema.IDataType FDataType;
		public virtual Schema.IDataType DataType
		{
			get { return FDataType; }
			set { FDataType = value; }
		}
		
        // DetermineDataType
        public virtual void DetermineDataType(Plan APlan) {}
        
        // DetermineModifiers
        protected virtual void DetermineModifiers(Plan APlan) 
        {
			if (Modifiers != null)
			{
				IgnoreUnsupported = Boolean.Parse(LanguageModifiers.GetModifier(Modifiers, "IgnoreUnsupported", IgnoreUnsupported.ToString()));
				ShouldSupport = Boolean.Parse(LanguageModifiers.GetModifier(Modifiers, "ShouldSupport", ShouldSupport.ToString()));
				ShouldEmitIL = FShouldEmitIL && (Boolean.Parse(LanguageModifiers.GetModifier(Modifiers, "ShouldEmitIL", ShouldEmitIL.ToString())));
			}
        }

		// DetermineCharacteristics        
		public virtual void DetermineCharacteristics(Plan APlan) {}
		
        // DetermineBinding
        public virtual void DetermineBinding(Plan APlan)
        {
			if (FDataType != null)
				APlan.PushTypeContext(FDataType);
			try
			{
				InternalDetermineBinding(APlan);
			}
			finally
			{
				if (FDataType != null)
					APlan.PopTypeContext(FDataType);
			}
			
			DetermineDevice(APlan);
        }

		public virtual void InternalDetermineBinding(Plan APlan)
		{
			for (int LIndex = 0; LIndex < Nodes.Count; LIndex++)
				Nodes[LIndex].DetermineBinding(APlan);
		}
		
		/// <summary>Rechecks security for the plan using the given plan and associated security context.</summary>
		public virtual void BindToProcess(Plan APlan)
		{
			for (int LIndex = 0; LIndex < Nodes.Count; LIndex++)
				Nodes[LIndex].BindToProcess(APlan);
		}
		
		protected virtual DataVar ProtectedExecute(ServerProcess AProcess)
		{
			if (FDeviceSupported)
				return AProcess.DeviceExecute(FDevice, this);
			else
				return InternalExecute(AProcess);
		}
		
		protected DataVar StandardExecute(ServerProcess AProcess)
		{
			#if WRAPRUNTIMEEXCEPTIONS
			try
			{
			#endif
				AProcess.CheckAborted();
				AProcess.Context.IncCallDepth();
				try
				{
					return ProtectedExecute(AProcess);
				}
				finally
				{
					AProcess.Context.DecCallDepth();
				}
			#if WRAPRUNTIMEEXCEPTIONS
			}
			catch (RuntimeException LRuntimeException)
			{
				if (!LRuntimeException.HasContext())
					LRuntimeException.SetContext(this);
				throw LRuntimeException;
			}
			catch (ControlError LControlError)
			{
				throw LControlError;
			}
			catch (NullReferenceException LNullReferenceException)
			{
				throw new RuntimeException(RuntimeException.Codes.NilEncountered, LNullReferenceException, this);
			}
			catch (DataphorException LDataphorException)
			{
				if ((LDataphorException.Severity == ErrorSeverity.User) || (LDataphorException.ServerContext != null) || (LDataphorException.Code == (int)RuntimeException.Codes.RuntimeError))
					throw LDataphorException;
				else
					throw new RuntimeException(RuntimeException.Codes.RuntimeError, LDataphorException.Severity, LDataphorException, this, LDataphorException.Message);
			}
			catch (FormatException LFormatException)
			{
				throw new DataphorException(ErrorSeverity.User, DataphorException.CApplicationError, LFormatException.Message, LFormatException);
			}
			catch (ArgumentException LArgumentException)
			{
				throw new DataphorException(ErrorSeverity.User, DataphorException.CApplicationError, LArgumentException.Message, LArgumentException);
			}
			catch (ArithmeticException LArithmeticException)
			{
				throw new DataphorException(ErrorSeverity.User, DataphorException.CApplicationError, LArithmeticException.Message, LArithmeticException);
			}
			catch (Exception LException)
			{
				throw new RuntimeException(RuntimeException.Codes.RuntimeError, ErrorSeverity.Application, LException, this, LException.Message);
			}
			#endif
		}
		
        // Execute
        public ExecuteDelegate Execute;
        
        public abstract DataVar InternalExecute(ServerProcess AProcess);

        public virtual DataVar InternalExecute(ServerProcess AProcess, DataVar[] AArguments)
        {
			throw new RuntimeException(RuntimeException.Codes.InstructionExecuteNotSupported, GetType().Name);
        }

		#region ShowPlan

		public virtual string Description
		{
			get 
			{
				string LName = GetType().Name;
				if (LName.EndsWith("Node"))
					return LName.Substring(0, LName.Length - 4);
				else
					return LName;
			}
		}

		public virtual string Category
		{
			get { return "Unknown"; }
		}

		protected virtual void WritePlanElement(System.Xml.XmlWriter AWriter)
		{
			AWriter.WriteStartElement(GetType().Name);
		}

		protected virtual void WritePlanNodes(System.Xml.XmlWriter AWriter)
		{
			foreach (PlanNode LNode in Nodes)
				LNode.WritePlan(AWriter);
		}

		protected static void WritePlanTags(System.Xml.XmlWriter AWriter, MetaData AMetaData)
		{
			if (AMetaData != null)
			{
				#if USEHASHTABLEFORTAGS
				foreach (Tag LTag in AMetaData.Tags)
				{
				#else
				Tag LTag;
				for (int LIndex = 0; LIndex < AMetaData.Tags.Count; LIndex++)
				{
					LTag = AMetaData.Tags[LIndex];
				#endif
					AWriter.WriteStartElement("Tags.Tag");
					AWriter.WriteAttributeString("Name", LTag.Name);
					AWriter.WriteAttributeString("Value", LTag.Value);
					AWriter.WriteEndElement();
				}
			}
		}

		protected virtual void WritePlanAttributes(System.Xml.XmlWriter AWriter)
		{
			AWriter.WriteAttributeString("ID", GetHashCode().ToString());
			AWriter.WriteAttributeString("Statement", SafeEmitStatementAsString(false));
			AWriter.WriteAttributeString("Class", GetType().FullName);
			AWriter.WriteAttributeString("Description", Description);
			AWriter.WriteAttributeString("Category", Category);
			if (FDataType != null)
				AWriter.WriteAttributeString("Type", FDataType.Name);
			AWriter.WriteAttributeString("Characteristics", CharacteristicsToString(this));
			AWriter.WriteAttributeString("DeviceSupported", Convert.ToString(DeviceSupported));
			if (DeviceSupported)
				AWriter.WriteAttributeString("Device", FDevice.DisplayName);
			AWriter.WriteAttributeString("CouldSupport", Convert.ToString(FCouldSupport));
			AWriter.WriteAttributeString("ShouldSupport", Convert.ToString(FShouldSupport));
			AWriter.WriteAttributeString("IgnoreUnsupported", Convert.ToString(FIgnoreUnsupported));
			AWriter.WriteAttributeString("DeviceAssociative", Convert.ToString((FDevice == null) && !FNoDevice));
			if (FDeviceMessages != null)
				AWriter.WriteAttributeString("DeviceMessages", FDeviceMessages.ToString());
		}

        public virtual void WritePlan(System.Xml.XmlWriter AWriter)
        {
			WritePlanElement(AWriter);
			WritePlanAttributes(AWriter);
			WritePlanNodes(AWriter);
			AWriter.WriteEndElement();
        }

		#endregion
	}
	
	public class PlanNodes : System.Object, IList
	{
		public const int CInitialCapacity = 4;
		
		public PlanNodes() : base(){}

		private int FCount;
		private PlanNode[] FNodes = new PlanNode[CInitialCapacity];
		
        public PlanNode this[int AIndex]
        {	
			get { return FNodes[AIndex]; }
			set { FNodes[AIndex] = value; }
        }
        
        private void EnsureCapacity(int ARequiredCapacity)
        {
			if (FNodes.Length <= ARequiredCapacity)
			{
				PlanNode[] FNewNodes = new PlanNode[FNodes.Length * 2];
				for (int LIndex = 0; LIndex < FNodes.Length; LIndex++)
					FNewNodes[LIndex] = FNodes[LIndex];
				FNodes = FNewNodes;
			}
        }
        
        public int Add(PlanNode AValue)
        {
			EnsureCapacity(FCount);
			FNodes[FCount] = AValue;
			FCount++;
			return FCount - 1;
        }
        
        public void Insert(int AIndex, PlanNode AValue)
        {
			EnsureCapacity(FCount);
			for (int LIndex = FCount - 1; LIndex >= AIndex; LIndex--)
				FNodes[LIndex + 1] = FNodes[LIndex];
			FNodes[AIndex] = AValue;
			FCount++;
        }

        public PlanNode Remove(PlanNode AValue)
        {
			return RemoveAt(IndexOf(AValue));
        }
        
        public PlanNode RemoveAt(int AIndex)
        {
			PlanNode LNode = FNodes[AIndex];
			FCount--;
			for (int LIndex = AIndex; LIndex < FCount; LIndex++)
				FNodes[LIndex] = FNodes[LIndex + 1];
			FNodes[FCount] = null; // Clear the last item to prevent a resource leak
			return LNode;
        }
        
        public void Clear()
        {
			while (FCount > 0)
				RemoveAt(FCount - 1);
        }
        
        public bool Contains(PlanNode AValue)
        {
			return IndexOf(AValue) >= 0;
        }

        public int IndexOf(object AValue)
        {
			for (int LIndex = 0; LIndex < FCount; LIndex++)
				if (FNodes[LIndex] == AValue)
					return LIndex;
			return -1;
        }

        public int Count { get { return FCount; } }

        // IEnumerable interface
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        public PlanNodeEnumerator GetEnumerator()
        {
			return new PlanNodeEnumerator(this);
        }

        public class PlanNodeEnumerator : IEnumerator
        {
            public PlanNodeEnumerator(PlanNodes APlanNodes) : base()
            {
                FPlanNodes = APlanNodes;
            }
            
            private int FCurrent = -1;
            private PlanNodes FPlanNodes;

            public PlanNode Current { get { return FPlanNodes[FCurrent]; } }
            
            object IEnumerator.Current { get { return FPlanNodes[FCurrent]; } }

            public void Reset()
            {
                FCurrent = -1;
            }

            public bool MoveNext()
            {
				FCurrent++;
				return FCurrent < FPlanNodes.Count;
            }
        }
        
        // ICollection
        void ICollection.CopyTo(System.Array AArray, int AIndex)
        {
			IList LList = (IList)AArray;
			for (int LIndex = 0; LIndex < Count; LIndex++)
				LList[AIndex + LIndex] = this[LIndex];
        }
        
        bool ICollection.IsSynchronized { get { return false; } }
        
        object ICollection.SyncRoot { get { return this; } }
        
        // IList
        int IList.Add(object AValue)
        {
			return Add((PlanNode)AValue);
        }
        
        bool IList.Contains(object AValue)
        {
			return IndexOf(AValue) >= 0;
        }
        
        void IList.Insert(int AIndex, object AValue)
        {
			Insert(AIndex, (PlanNode)AValue);
        }
        
        bool IList.IsFixedSize { get { return false; } }
        
        bool IList.IsReadOnly { get { return false; } }
        
        void IList.Remove(object AValue)
        {
			Remove((PlanNode)AValue);
        }
        
        void IList.RemoveAt(int AIndex)
        {
			RemoveAt(AIndex);
        }
        
        object IList.this[int AIndex]
        {
			get { return this[AIndex]; }
			set { this[AIndex] = (PlanNode)value; }
        }
	}
	
	public class DevicePlanNode : System.Object
	{
		public DevicePlanNode(PlanNode ANode) : base()
		{
			FNode = ANode;
		}
		
		protected PlanNode FNode;
		public PlanNode Node { get { return FNode; } }
	}
}
