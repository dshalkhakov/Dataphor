/*
	Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/
#define UseReferenceDerivation
#define NILPROPOGATION
	
namespace Alphora.Dataphor.DAE.Runtime.Instructions
{
	using System;
	using System.Text;
	using System.Threading;
	using System.Collections;

	using Alphora.Dataphor;
	using Alphora.Dataphor.DAE;
	using Alphora.Dataphor.DAE.Server;	
	using Alphora.Dataphor.DAE.Language;
	using Alphora.Dataphor.DAE.Language.D4;
	using Alphora.Dataphor.DAE.Runtime;
	using Alphora.Dataphor.DAE.Runtime.Data;
	using Alphora.Dataphor.DAE.Runtime.Instructions;
	using Alphora.Dataphor.DAE.Device.ApplicationTransaction;
	using Schema = Alphora.Dataphor.DAE.Schema;
	
    public class RowNode : PlanNode
    {
		// constructor
		public RowNode() : base(){}
		
		public new Schema.IRowType DataType
		{
			get { return (Schema.IRowType)FDataType; }
			set { FDataType = value; }
		}
		
		private Schema.IRowType FSpecifiedRowType;		
		/// <summary>If this is a specified row selector, this is the specified subset of the row selector.</summary>
		public Schema.IRowType SpecifiedRowType
		{
			get { return FSpecifiedRowType; }
			set { FSpecifiedRowType = value; }
		}

		public override void DetermineCharacteristics(Plan APlan)
		{
			FIsLiteral = true;
			FIsFunctional = true;
			FIsDeterministic = true;
			FIsRepeatable = true;
			FIsNilable = false;
			for (int LIndex = 0; LIndex < Nodes.Count; LIndex++)
			{
				FIsLiteral = FIsLiteral && Nodes[LIndex].IsLiteral;
				FIsFunctional = FIsFunctional && Nodes[LIndex].IsFunctional;
				FIsDeterministic = FIsDeterministic && Nodes[LIndex].IsDeterministic;
				FIsRepeatable = FIsRepeatable && Nodes[LIndex].IsRepeatable;
			} 
		}
		
		// Evaluate
		public override DataVar InternalExecute(ServerProcess AProcess)
		{
			Row LRow = new Row(AProcess, DataType);
			try
			{
				if (FSpecifiedRowType != null)
				{
					for (int LIndex = 0; LIndex < FSpecifiedRowType.Columns.Count; LIndex++)
					{
						DataVar LObject = Nodes[LIndex].Execute(AProcess);
						if ((LObject != null) && (LObject.Value != null)) 
						{
							try
							{
								if (!LObject.Value.IsNil)
								{
									if (LObject.DataType is Schema.ScalarType)
										((Schema.ScalarType)LObject.DataType).ValidateValue(AProcess, LObject);
									LRow[DataType.Columns.IndexOfName(FSpecifiedRowType.Columns[LIndex].Name)] = LObject.Value;
								}
							}
							finally
							{
								LObject.Value.Dispose();
							}
						}
					}
				}
				else
				{
					for (int LIndex = 0; LIndex < DataType.Columns.Count; LIndex++)
					{
						DataVar LObject = Nodes[LIndex].Execute(AProcess);
						if ((LObject != null) && (LObject.Value != null))
						{
							try
							{
								if (!LObject.Value.IsNil)
								{
									if (LObject.DataType is Schema.ScalarType)
										((Schema.ScalarType)LObject.DataType).ValidateValue(AProcess, LObject);
									LRow[LIndex] = LObject.Value;
								}
							}
							finally
							{
								LObject.Value.Dispose();
							}
						}
					}
				}
				return new DataVar(String.Empty, FDataType, LRow);
			}
			catch
			{
				LRow.Dispose();
				throw;
			}
		}
		
		// Statement
		public override Statement EmitStatement(EmitMode AMode)
		{
			if (FDataType is Schema.RowType)
			{
				RowSelectorExpression LExpression = new RowSelectorExpression();
				if (FSpecifiedRowType != null)
					LExpression.TypeSpecifier = DataType.EmitSpecifier(AMode);
				Schema.IRowType LRowType = FSpecifiedRowType != null ? FSpecifiedRowType : DataType;
				for (int LIndex = 0; LIndex < LRowType.Columns.Count; LIndex++)
					LExpression.Expressions.Add(new NamedColumnExpression((Expression)Nodes[LIndex].EmitStatement(AMode), LRowType.Columns[LIndex].Name));
				LExpression.Modifiers = Modifiers;
				return LExpression;
			}
			else
			{
				EntrySelectorExpression LExpression = new EntrySelectorExpression();
				for (int LIndex = 0; LIndex < DataType.Columns.Count; LIndex++)
					LExpression.Expressions.Add(new NamedColumnExpression((Expression)Nodes[LIndex].EmitStatement(AMode), DataType.Columns[LIndex].Name));
				LExpression.Modifiers = Modifiers;
				return LExpression;
			}
		}
	}

    public class RowSelectorNode : RowNode
    {
		public RowSelectorNode(Schema.IRowType ADataType) : base()
		{
			FDataType = ADataType;
		}
		
		public override void DetermineDataType(Plan APlan)
		{
			Error.Fail("Unimplemented RowSelectorNode.DetermineDataType");
		}
    }
    
    public class RowExtendNode : UnaryRowNode
    {
		protected int FExtendColumnOffset;		
		public int ExtendColumnOffset { get { return FExtendColumnOffset; } }
		
		private NamedColumnExpressions FExpressions;
		public NamedColumnExpressions Expressions
		{
			get { return FExpressions; }
			set { FExpressions = value; }
		}
		
		public override void DetermineDataType(Plan APlan)
		{
			DetermineModifiers(APlan);
			FDataType = new Schema.RowType();

			foreach (Schema.Column LColumn in SourceRowType.Columns)
				DataType.Columns.Add(LColumn.Copy());
			FExtendColumnOffset = DataType.Columns.Count;

			APlan.EnterRowContext();
			try
			{			
				APlan.Symbols.Push(new DataVar(SourceRowType));
				try
				{
					// Add a column for each expression
					PlanNode LPlanNode;
					Schema.Column LNewColumn;
					foreach (NamedColumnExpression LColumn in FExpressions)
					{
						LPlanNode = Compiler.CompileExpression(APlan, LColumn.Expression);

						LNewColumn = 
							new Schema.Column
							(
								LColumn.ColumnAlias, 
								LPlanNode.DataType
							);
							
						DataType.Columns.Add(LNewColumn);
						Nodes.Add(LPlanNode);
					}
				}
				finally
				{
					APlan.Symbols.Pop();
				}
			}
			finally
			{
				APlan.ExitRowContext();
			}
		}
		
		public override void InternalDetermineBinding(Plan APlan)
		{
			Nodes[0].DetermineBinding(APlan);
			APlan.EnterRowContext();
			try
			{
				APlan.Symbols.Push(new DataVar(SourceRowType));
				try
				{
					for (int LIndex = 1; LIndex < Nodes.Count; LIndex++)
						Nodes[LIndex].DetermineBinding(APlan);
				}
				finally
				{
					APlan.Symbols.Pop();
				}
			}
			finally
			{
				APlan.ExitRowContext();
			}
		}
		
		public override Statement EmitStatement(EmitMode AMode)
		{
			ExtendExpression LExpression = new ExtendExpression();
			LExpression.Expression = (Expression)Nodes[0].EmitStatement(AMode);
			for (int LIndex = ExtendColumnOffset; LIndex < DataType.Columns.Count; LIndex++)
				LExpression.Expressions.Add(new NamedColumnExpression((Expression)Nodes[LIndex - ExtendColumnOffset + 1].EmitStatement(AMode), DataType.Columns[LIndex].Name));
			LExpression.Modifiers = Modifiers;
			return LExpression;
		}
		
		public override DataVar InternalExecute(ServerProcess AProcess)
		{
			DataVar LResult;
			Row LSourceRow = (Row)Nodes[0].Execute(AProcess).Value;
			#if NILPROPOGATION
			if ((LSourceRow == null) || LSourceRow.IsNil)
				return new DataVar(FDataType, null);
			#endif
			try
			{
				Row LNewRow = new Row(AProcess, DataType);
				try
				{
					AProcess.Context.Push(new DataVar(LSourceRow.DataType, LSourceRow));
					try
					{
						LSourceRow.CopyTo(LNewRow);
						
						for (int LIndex = FExtendColumnOffset; LIndex < LNewRow.DataType.Columns.Count; LIndex++)
						{
							LResult = Nodes[LIndex - FExtendColumnOffset + 1].Execute(AProcess);
							if ((LResult.Value != null) && !LResult.Value.IsNil)
							{
								try
								{
									LNewRow[LIndex] = LResult.Value;
								}
								finally
								{
									LResult.Value.Dispose();
								}
							}
						}
					}
					finally
					{
						AProcess.Context.Pop();
					}
					return new DataVar(LNewRow.DataType, LNewRow);
				}
				catch
				{
					LNewRow.Dispose();
					throw;
				}
			}
			finally
			{
				LSourceRow.Dispose();
			}
		}

		public override bool IsContextLiteral(int ALocation)
		{
			if (!Nodes[0].IsContextLiteral(ALocation))
				return false;
			for (int LIndex = 1; LIndex < Nodes.Count; LIndex++)
				if (!Nodes[LIndex].IsContextLiteral(ALocation + 1))
					return false;
			return true;
		}
    }
    
	public class RowRedefineNode : UnaryRowNode
	{
		protected int[] FRedefineColumnOffsets;		
		public int[] RedefineColumnOffsets
		{
			get { return FRedefineColumnOffsets; }
			set { FRedefineColumnOffsets = value; }
		}
		
		private NamedColumnExpressions FExpressions;
		public NamedColumnExpressions Expressions
		{
			get { return FExpressions; }
			set { FExpressions = value; }
		}
		
		public override void DetermineDataType(Plan APlan)
		{
			DetermineModifiers(APlan);
			FDataType = new Schema.RowType();

			foreach (Schema.Column LColumn in SourceRowType.Columns)
				DataType.Columns.Add(LColumn.Copy());

			int LIndex = 0;			
			FRedefineColumnOffsets = new int[FExpressions.Count];
			APlan.EnterRowContext();
			try
			{
				APlan.Symbols.Push(new DataVar(SourceRowType));
				try
				{
					// Add a column for each expression
					PlanNode LPlanNode;
					Schema.Column LSourceColumn;
					Schema.Column LNewColumn;
					foreach (NamedColumnExpression LColumn in FExpressions)
					{
						int LSourceColumnIndex = DataType.Columns.IndexOf(LColumn.ColumnAlias);
						if (LSourceColumnIndex < 0)
							throw new CompilerException(CompilerException.Codes.UnknownIdentifier, LColumn, LColumn.ColumnAlias);
							
						LSourceColumn = DataType.Columns[LSourceColumnIndex];
						LPlanNode = Compiler.CompileExpression(APlan, LColumn.Expression);
						LNewColumn = new Schema.Column(LSourceColumn.Name, LPlanNode.DataType);
						DataType.Columns[LSourceColumnIndex] = LNewColumn;
						FRedefineColumnOffsets[LIndex] = LSourceColumnIndex;
						Nodes.Add(LPlanNode);
						LIndex++;
					}
				}
				finally
				{
					APlan.Symbols.Pop();
				}
			}
			finally
			{
				APlan.ExitRowContext();
			}
		}
		
		public override Statement EmitStatement(EmitMode AMode)
		{
			RedefineExpression LExpression = new RedefineExpression();
			LExpression.Expression = (Expression)Nodes[0].EmitStatement(AMode);
			for (int LIndex = 0; LIndex < FRedefineColumnOffsets.Length; LIndex++)
				LExpression.Expressions.Add(new NamedColumnExpression((Expression)Nodes[LIndex + 1].EmitStatement(AMode), DataType.Columns[FRedefineColumnOffsets[LIndex]].Name));
			LExpression.Modifiers = Modifiers;
			return LExpression;
		}
		
		public override void InternalDetermineBinding(Plan APlan)
		{
			Nodes[0].DetermineBinding(APlan);
			APlan.EnterRowContext();
			try
			{
				APlan.Symbols.Push(new DataVar(SourceRowType));
				try
				{
					for (int LIndex = 1; LIndex < Nodes.Count; LIndex++)
						Nodes[LIndex].DetermineBinding(APlan);
				}
				finally
				{
					APlan.Symbols.Pop();
				}
			}
			finally
			{
				APlan.ExitRowContext();
			}
		}
		
		public override DataVar InternalExecute(ServerProcess AProcess)
		{
			Row LSourceRow = (Row)Nodes[0].Execute(AProcess).Value;
			#if NILPROPOGATION
			if ((LSourceRow == null) || LSourceRow.IsNil)
				return new DataVar(FDataType, null);
			#endif
			try
			{
				AProcess.Context.Push(new DataVar(LSourceRow.DataType, LSourceRow));
				try
				{
					Row LNewRow = new Row(AProcess, DataType);
					try
					{
						int LColumnIndex;
						for (int LIndex = 0; LIndex < DataType.Columns.Count; LIndex++)
						{
							if (!((IList)FRedefineColumnOffsets).Contains(LIndex))
							{
								LColumnIndex = LSourceRow.DataType.Columns.IndexOfName(DataType.Columns[LIndex].Name);
								if (LSourceRow.HasValue(LColumnIndex))
									LNewRow[LIndex] = LSourceRow[LColumnIndex];
								else
									LNewRow.ClearValue(LIndex);
							}
						}
						
						for (int LIndex = 0; LIndex < FRedefineColumnOffsets.Length; LIndex++)
						{
							DataVar LResult = Nodes[LIndex + 1].Execute(AProcess);
							if ((LResult.Value != null) && !LResult.Value.IsNil)
							{
								try
								{
									LNewRow[FRedefineColumnOffsets[LIndex]] = LResult.Value;
								}
								finally
								{
									LResult.Value.Dispose();
								}
							}
							else
								LNewRow.ClearValue(FRedefineColumnOffsets[LIndex]);
						}
						return new DataVar(FDataType, LNewRow);
					}
					catch
					{
						LNewRow.Dispose();
						throw;
					}
				}
				finally
				{
					AProcess.Context.Pop();
				}
			}
			finally
			{
				LSourceRow.Dispose();
			}
		}
		
		public override bool IsContextLiteral(int ALocation)
		{
			if (!Nodes[0].IsContextLiteral(ALocation))
				return false;
			for (int LIndex = 1; LIndex < Nodes.Count; LIndex++)
				if (!Nodes[LIndex].IsContextLiteral(ALocation + 1))
					return false;
			return true;
		}
	}
	
	// operator Rename(row) : row
	public class RowRenameNode : UnaryRowNode
	{
		private string FRowAlias;
		public string RowAlias
		{
			get { return FRowAlias; }
			set { FRowAlias = value; }
		}
		
		private MetaData FMetaData;
		public MetaData MetaData
		{
			get { return FMetaData; }
			set { FMetaData = value; }
		}

		private RenameColumnExpressions FExpressions;		
		public RenameColumnExpressions Expressions
		{
			get { return FExpressions; }
			set { FExpressions = value; }
		}

		// Indicates whether this row rename should be emitted
		// This is used by the table-indexer compilation process to
		// ensure that the row rename portion is not re-emitted
		// as part of the expression
		private bool FShouldEmit = true;
		public bool ShouldEmit
		{
			get { return FShouldEmit; }
			set { FShouldEmit = value; }
		}
		
		public override void DetermineDataType(Plan APlan)
		{
			DetermineModifiers(APlan);
			FDataType = new Schema.RowType();

			if (FExpressions == null)
			{
				// Inherit columns
				foreach (Schema.Column LColumn in SourceRowType.Columns)
					DataType.Columns.Add(new Schema.Column(Schema.Object.Qualify(LColumn.Name, FRowAlias), LColumn.DataType));
			}
			else
			{
				bool LColumnAdded;
				Schema.Column LColumn;
				int LRenameColumnIndex;
				for (int LIndex = 0; LIndex < SourceRowType.Columns.Count; LIndex++)
				{
					LColumnAdded = false;
					foreach (RenameColumnExpression LRenameColumn in FExpressions)
					{
						LRenameColumnIndex = SourceRowType.Columns.IndexOf(LRenameColumn.ColumnName);
						if (LRenameColumnIndex < 0)
							throw new Schema.SchemaException(Schema.SchemaException.Codes.ObjectNotFound, LRenameColumn.ColumnName);
						else if (LRenameColumnIndex == LIndex)
						{
							LColumn = new Schema.Column(LRenameColumn.ColumnAlias, SourceRowType.Columns[LIndex].DataType);
							DataType.Columns.Add(LColumn);
							LColumnAdded = true;
							break;
						}
					}
					if (!LColumnAdded)
						DataType.Columns.Add(SourceRowType.Columns[LIndex].Copy());
				}
			}
		}
		
		public override DataVar InternalExecute(ServerProcess AProcess, DataVar[] AArguments)
		{
			Row LSourceRow = (Row)AArguments[0].Value;
			#if NILPROPOGATION
			if ((LSourceRow == null) || LSourceRow.IsNil)
				return new DataVar(FDataType, null);
			#endif
			try
			{
				Row LNewRow = new Row(AProcess, DataType);
				try
				{
					for (int LIndex = 0; LIndex < LSourceRow.DataType.Columns.Count; LIndex++)
						if (LSourceRow.HasValue(LIndex) && (LIndex < LNewRow.DataType.Columns.Count))
							LNewRow[LIndex] = LSourceRow[LIndex];
					
					return new DataVar(FDataType, LNewRow);
				}
				catch
				{
					LNewRow.Dispose();
					throw;
				}
			}
			finally
			{
				LSourceRow.Dispose();
			}
		}

		public override Statement EmitStatement(EmitMode AMode)
		{
			if (ShouldEmit && (DataType.Columns.Count > 0))
			{
				RenameExpression LExpression = new RenameExpression();
				LExpression.Expression = (Expression)Nodes[0].EmitStatement(AMode);
				for (int LIndex = 0; LIndex < DataType.Columns.Count; LIndex++)
					LExpression.Expressions.Add(new RenameColumnExpression(Schema.Object.EnsureRooted(SourceRowType.Columns[LIndex].Name), DataType.Columns[LIndex].Name));
				LExpression.Modifiers = Modifiers;
				return LExpression;
			}
			else
				return Nodes[0].EmitStatement(AMode);
		}
	}
	
	public class RowJoinNode : InstructionNode
	{
		private PlanNode FEqualNode;

		public new Schema.RowType DataType { get { return (Schema.RowType)FDataType; } }
		public Schema.RowType LeftRowType { get { return (Schema.RowType)Nodes[0].DataType; } }
		public Schema.RowType RightRowType { get { return (Schema.RowType)Nodes[1].DataType; } }
		
		public override void DetermineDataType(Plan APlan)
		{
			base.DetermineDataType(APlan);
			FDataType = new Schema.RowType();
			Expression LExpression = null;
			int LRightIndex;
			for (int LLeftIndex = 0; LLeftIndex < LeftRowType.Columns.Count; LLeftIndex++)
			{
				LRightIndex = RightRowType.Columns.IndexOfName(LeftRowType.Columns[LLeftIndex].Name);
				if (LRightIndex >= 0)
				{
					Expression LEqualExpression = 
						new BinaryExpression
						(
							new IdentifierExpression(Schema.Object.Qualify(LeftRowType.Columns[LLeftIndex].Name, Keywords.Left)), 
							Instructions.Equal, 
							new IdentifierExpression(Schema.Object.Qualify(RightRowType.Columns[LRightIndex].Name, Keywords.Right))
						);
						
					if (LExpression != null)
						LExpression = new BinaryExpression(LExpression, Instructions.And, LEqualExpression);
					else
						LExpression = LEqualExpression;
				}
				DataType.Columns.Add(LeftRowType.Columns[LLeftIndex].Copy());
			}
			
			for (int LIndex = 0; LIndex < RightRowType.Columns.Count; LIndex++)
				if (!LeftRowType.Columns.ContainsName(RightRowType.Columns[LIndex].Name))
					DataType.Columns.Add(RightRowType.Columns[LIndex].Copy());
			
			if (LExpression != null)
			{
				APlan.EnterRowContext();
				try
				{
					APlan.Symbols.Push(new DataVar(new Schema.RowType(LeftRowType.Columns, Keywords.Left)));
					try
					{
						APlan.Symbols.Push(new DataVar(new Schema.RowType(RightRowType.Columns, Keywords.Right)));
						try
						{
							FEqualNode = Compiler.CompileExpression(APlan, LExpression);
						}
						finally
						{
							APlan.Symbols.Pop();
						}
					}
					finally
					{
						APlan.Symbols.Pop();
					}
				}
				finally
				{
					APlan.ExitRowContext();
				}
			}
			else
				FEqualNode = null;
		}
		
		public override DataVar InternalExecute(ServerProcess AProcess, DataVar[] AArguments)
		{
			#if NILPROPOGATION
			if ((AArguments[0].Value == null) || AArguments[0].Value.IsNil || (AArguments[1].Value == null) || AArguments[1].Value.IsNil)
				return new DataVar(FDataType, null);
			#endif
			if (FEqualNode != null)
			{
				AProcess.Context.Push(AArguments[0]);
				try
				{
					AProcess.Context.Push(AArguments[1]);
					try
					{
						DataValue LValue = FEqualNode.Execute(AProcess).Value;
						if ((LValue == null) || LValue.IsNil || !LValue.AsBoolean)
							throw new RuntimeException(RuntimeException.Codes.InvalidRowJoin);
					}
					finally
					{
						AProcess.Context.Pop();
					}
				}
				finally
				{
					AProcess.Context.Pop();
				}
			}
			Row LResult = new Row(AProcess, DataType);
			try
			{
				((Row)AArguments[0].Value).CopyTo(LResult);
				((Row)AArguments[1].Value).CopyTo(LResult);
				return new DataVar(FDataType, LResult);
			}
			catch
			{	
				LResult.Dispose();
				throw;
			}
		}

		public override Statement EmitStatement(EmitMode AMode)
		{
			InnerJoinExpression LExpression = new InnerJoinExpression();
			LExpression.LeftExpression = (Expression)Nodes[0].EmitStatement(AMode);
			LExpression.RightExpression = (Expression)Nodes[1].EmitStatement(AMode);
			LExpression.Modifiers = Modifiers;
			return LExpression;
		}
	}
	
	public abstract class UnaryRowNode : InstructionNode
	{
		public new Schema.RowType DataType { get { return (Schema.RowType)FDataType; } }
		public Schema.RowType SourceRowType { get { return (Schema.RowType)Nodes[0].DataType; } }
	}
	
	public abstract class RowProjectNodeBase : UnaryRowNode
	{
		// ColumnNames
		protected TypedList FColumnNames = new TypedList(typeof(string), false);
		public TypedList ColumnNames { get { return FColumnNames; } }
		
		public override DataVar InternalExecute(ServerProcess AProcess, DataVar[] AArguments)
		{
			#if NILPROPOGATION
			if ((AArguments[0].Value == null) || AArguments[0].Value.IsNil)
				return new DataVar(FDataType, null);
			#endif
			
			Row LRow = new Row(AProcess, DataType);
			try
			{
				((Row)AArguments[0].Value).CopyTo(LRow);
				return new DataVar(FDataType, LRow);
			}
			catch
			{
				LRow.Dispose();
				throw;
			}
		}
	}
	
	public class RowProjectNode : RowProjectNodeBase
	{
		public override void DetermineDataType(Plan APlan)
		{
			DetermineModifiers(APlan);
			FDataType = new Schema.RowType();
			foreach (string LColumnName in FColumnNames)
				DataType.Columns.Add(SourceRowType.Columns[LColumnName].Copy());
		}

		public override Statement EmitStatement(EmitMode AMode)
		{
			ProjectExpression LExpression = new ProjectExpression();
			LExpression.Expression = (Expression)Nodes[0].EmitStatement(AMode);
			foreach (Schema.Column LColumn in DataType.Columns)
				LExpression.Columns.Add(new ColumnExpression(Schema.Object.EnsureRooted(LColumn.Name)));
			LExpression.Modifiers = Modifiers;
			return LExpression;
		}
	}
	
	public class RowRemoveNode : RowProjectNodeBase
	{
		public override void DetermineDataType(Plan APlan)
		{
			DetermineModifiers(APlan);
			FDataType = new Schema.RowType();
			
			foreach (Schema.Column LColumn in SourceRowType.Columns)
				DataType.Columns.Add(LColumn.Copy());
				
			foreach (string LColumnName in FColumnNames)
				DataType.Columns.Remove(DataType.Columns[LColumnName]);
		}

		public override Statement EmitStatement(EmitMode AMode)
		{
			RemoveExpression LExpression = new RemoveExpression();
			LExpression.Expression = (Expression)Nodes[0].EmitStatement(AMode);
			foreach (Schema.Column LColumn in SourceRowType.Columns)
				if (!DataType.Columns.ContainsName(LColumn.Name))
					LExpression.Columns.Add(new ColumnExpression(Schema.Object.EnsureRooted(LColumn.Name)));
			LExpression.Modifiers = Modifiers;
			return LExpression;
		}
	}
}