/*
	Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/
#define NILPROPOGATION

using System;

namespace Alphora.Dataphor.DAE.Runtime.Instructions
{
	using Alphora.Dataphor.DAE.Compiling;
	using Alphora.Dataphor.DAE.Compiling.Visitors;
	using Alphora.Dataphor.DAE.Language;
	using Alphora.Dataphor.DAE.Language.D4;
	using Alphora.Dataphor.DAE.Runtime;
	using Alphora.Dataphor.DAE.Runtime.Data;
	using Schema = Alphora.Dataphor.DAE.Schema;

	public class ListNode : PlanNode
	{
		// ListType
		public Schema.ListType ListType { get { return (Schema.ListType)_dataType; } } 
		
		// DetermineDataType not used, ListNode.ListType is set by the compiler
		
		public override void DetermineCharacteristics(Plan plan)
		{
			_isLiteral = true;
			_isFunctional = true;
			_isDeterministic = true;
			_isRepeatable = true;
			_isNilable = false;
			for (int index = 0; index < NodeCount; index++)
			{
				_isLiteral = _isLiteral && Nodes[index].IsLiteral;
				_isFunctional = _isFunctional && Nodes[index].IsFunctional;
				_isDeterministic = _isDeterministic && Nodes[index].IsDeterministic;
				_isRepeatable = _isRepeatable && Nodes[index].IsRepeatable;
			} 
		}
		
		// Evaluate
		public override object InternalExecute(Program program)
		{
			ListValue list = new ListValue(program.ValueManager, ListType);
			for (int index = 0; index < NodeCount; index++)
				list.Add(Nodes[index].Execute(program));
			return list;
		}
		
		public override Statement EmitStatement(EmitMode mode)
		{
			ListSelectorExpression expression = new ListSelectorExpression();
			expression.TypeSpecifier = ListType.EmitSpecifier(mode);
			for (int index = 0; index < NodeCount; index++)
				expression.Expressions.Add(Nodes[index].EmitStatement(mode));
			expression.Modifiers = Modifiers;
			return expression;
		}
	}

	// operator iIndexer(const AList : list, const AIndex : integer) : generic
	public class IndexerNode : BinaryInstructionNode
	{
		public bool ByReference;
		
		public override void DetermineDataType(Plan plan)
		{
			base.DetermineDataType(plan);
			_dataType = ((Schema.ListType)Nodes[0].DataType).ElementType;
		}
		
		public override object InternalExecute(Program program, object argument1, object argument2)
		{
			#if NILPROPOGATION
			if (argument1 == null || argument2 == null)
				return null;
			#endif
			
			if (ByReference)
				return ((ListValue)argument1)[(int)argument2];

			return DataValue.CopyValue(program.ValueManager, ((ListValue)argument1)[(int)argument2]);
		}

		public override Statement EmitStatement(EmitMode mode)
		{
			D4IndexerExpression expression = new D4IndexerExpression();
			expression.Expression = (Expression)Nodes[0].EmitStatement(mode);
			expression.Indexer = (Expression)Nodes[1].EmitStatement(mode);
			expression.Modifiers = Modifiers;
			return expression;
		}

		protected override void InternalClone(PlanNode newNode)
		{
			base.InternalClone(newNode);

			var newIndexerNode = (IndexerNode)newNode;
			newIndexerNode.ByReference = ByReference;
		}
	}
	
	// operator Count(const AList : list) : integer;	
	public class ListCountNode : UnaryInstructionNode
	{
		public override object InternalExecute(Program program, object argument1)
		{
			#if NILPROPOGATION
			if (argument1 == null)
				return null;
			#endif
			
			return ((ListValue)argument1).Count();
		}
	}
	
	// operator Clear(var AList : list);
	public class ListClearNode : UnaryInstructionNode
	{
		public override object InternalExecute(Program program, object argument1)
		{
			((ListValue)argument1).Clear();
			return null;
		}
	}
	
	// operator Add(var AList : list, AValue : generic) : integer;
	public class ListAddNode : BinaryInstructionNode
	{
		public override void DetermineDataType(Plan plan)
		{
			if (!Nodes[1].DataType.Is(((Schema.IListType)Nodes[0].DataType).ElementType))
			{
				ConversionContext context = Compiler.FindConversionPath(plan, Nodes[1].DataType, ((Schema.IListType)Nodes[0].DataType).ElementType);
				Compiler.CheckConversionContext(plan, context);
				Nodes[1] = Compiler.ConvertNode(plan, Nodes[1], context);
			}

			Nodes[1] = Compiler.Upcast(plan, Nodes[1], ((Schema.IListType)Nodes[0].DataType).ElementType);
			base.DetermineDataType(plan);
		}
		
		public override object InternalExecute(Program program, object argument1, object argument2)
		{
			return ((ListValue)argument1).Add(argument2);
		}
	}
	
	// operator Insert(var AList : list, AValue : generic, AIndex : integer);
	public class ListInsertNode : TernaryInstructionNode
	{
		public override void DetermineDataType(Plan plan)
		{
			if (!Nodes[1].DataType.Is(((Schema.IListType)Nodes[0].DataType).ElementType))
			{
				ConversionContext context = Compiler.FindConversionPath(plan, Nodes[1].DataType, ((Schema.IListType)Nodes[0].DataType).ElementType);
				Compiler.CheckConversionContext(plan, context);
				Nodes[1] = Compiler.ConvertNode(plan, Nodes[1], context);
			}

			Nodes[1] = Compiler.Upcast(plan, Nodes[1], ((Schema.IListType)Nodes[0].DataType).ElementType);
			base.DetermineDataType(plan);
		}
		
		public override object InternalExecute(Program program, object argument1, object argument2, object argument3)
		{
			((ListValue)argument1).Insert((int)argument3, argument2);
			return null;
		}
	}
	
	// operator RemoveAt(var AList : list, const AIndex : integer);
	public class ListRemoveAtNode : BinaryInstructionNode
	{
		public override object InternalExecute(Program program, object argument1, object argument2)
		{
			((ListValue)argument1).RemoveAt((int)argument2);
			return null;
		}
	}
	
	public abstract class BaseListIndexOfNode : InstructionNode
	{
		public PlanNode _equalNode; // The equality node used to compare each item in the list against AValue
		
		public override void DetermineDataType(Plan plan)
		{
			DetermineModifiers(plan);
			plan.Symbols.Push(new Symbol("AValue", Nodes[1].DataType));
			try
			{
				plan.Symbols.Push(new Symbol("ACompareValue", ((Schema.ListType)Nodes[0].DataType).ElementType));
				try
				{
					_equalNode = Compiler.CompileExpression(plan, new BinaryExpression(new IdentifierExpression("AValue"), Instructions.Equal, new IdentifierExpression("ACompareValue")));
				}
				finally
				{
					plan.Symbols.Pop();
				}
			}
			finally
			{
				plan.Symbols.Pop();
			}
		}
		
		protected override void InternalBindingTraversal(Plan plan, PlanNodeVisitor visitor)
		{
			base.InternalBindingTraversal(plan, visitor);
			plan.Symbols.Push(new Symbol("AValue", Nodes[1].DataType));
			try
			{
				plan.Symbols.Push(new Symbol("ACompareValue", ((Schema.ListType)Nodes[0].DataType).ElementType));
				try
				{
					#if USEVISIT
					_equalNode = visitor.Visit(plan, _equalNode);
					#else
					_equalNode.BindingTraversal(plan, visitor);
					#endif
				}
				finally
				{
					plan.Symbols.Pop();
				}
			}
			finally
			{
				plan.Symbols.Pop();
			}
		}

		protected object InternalSearch(Program program, ListValue list, object tempValue, int startIndex, int length, int incrementor)
		{
			if (length < 0)
				throw new RuntimeException(RuntimeException.Codes.InvalidLength, ErrorSeverity.Application);
			if (length == 0)
				return -1;

			int localStartIndex = Math.Max(Math.Min(startIndex, list.Count() - 1), 0);
			int endIndex = Math.Max(Math.Min(startIndex + ((length - 1) * incrementor), list.Count() - 1), 0);
			
			program.Stack.Push(tempValue);
			try
			{
				int index = localStartIndex;
				while (((incrementor > 0) && (index <= endIndex)) || ((incrementor < 0) && (index >= endIndex)))
				{
					program.Stack.Push(list[index]);
					try
					{
						object localTempValue = _equalNode.Execute(program);
						if ((localTempValue != null) && (bool)localTempValue)
							return index;
					}
					finally
					{
						program.Stack.Pop();
					}
					index += incrementor;
				}
			}
			finally
			{
				program.Stack.Pop();
			}

			return -1;
		}

		protected override void InternalClone(PlanNode newNode)
		{
			base.InternalClone(newNode);

			if (_equalNode != null)
			{
				var newBaseListIndexOfNode = (BaseListIndexOfNode)newNode;
				newBaseListIndexOfNode._equalNode = _equalNode.Clone();
			}
		}
	}

	// operator IndexOf(const AList : list, const AValue : generic);
	public class ListIndexOfNode : BaseListIndexOfNode
	{
		public override object InternalExecute(Program program, object[] arguments)
		{
			#if NILPROPOGATION
			if (arguments[0] == null || arguments[1] == null)
				return null;
			#endif
			
			ListValue list = (ListValue)arguments[0];
			return InternalSearch(program, list, arguments[1], 0, list.Count(), 1);
		}
	}

	// operator IndexOf(const AList : list, const AValue : generic, const AStartIndex : Integer);
	public class ListIndexOfStartNode : BaseListIndexOfNode
	{
		public override object InternalExecute(Program program, object[] arguments)
		{
			#if NILPROPOGATION
			if (arguments[0] == null || arguments[1] == null || arguments[2] == null)
				return null;
			#endif
			
			ListValue list = (ListValue)arguments[0];
			return InternalSearch(program, list, arguments[1], (int)arguments[2], list.Count(), 1);
		}
	}

	// operator IndexOf(const AList : list, const AValue : generic, const AStartIndex : Integer, const ALength : Integer);
	public class ListIndexOfStartLengthNode : BaseListIndexOfNode
	{
		public override object InternalExecute(Program program, object[] arguments)
		{
			#if NILPROPOGATION
			if (arguments[0] == null || arguments[1] == null || arguments[2] == null || arguments[3] == null)
				return null;
			#endif
			
			return InternalSearch(program, (ListValue)arguments[0], arguments[1], (int)arguments[2], (int)arguments[3], 1);
		}
	}

	// operator LastIndexOf(const AList : list, const AValue : generic);
	public class ListLastIndexOfNode : BaseListIndexOfNode
	{
		public override object InternalExecute(Program program, object[] arguments)
		{
			#if NILPROPOGATION
			if (arguments[0] == null || arguments[1] == null)
				return null;
			#endif
			
			ListValue list = (ListValue)arguments[0];
			return InternalSearch(program, list, arguments[1], list.Count() - 1, list.Count(), -1);
		}
	}

	// operator LastIndexOf(const AList : list, const AValue : generic, const AStartIndex : Integer);
	public class ListLastIndexOfStartNode : BaseListIndexOfNode
	{
		public override object InternalExecute(Program program, object[] arguments)
		{
			#if NILPROPOGATION
			if (arguments[0] == null || arguments[1] == null || arguments[2] == null)
				return null;
			#endif
			
			ListValue list = (ListValue)arguments[0];
			return InternalSearch(program, list, arguments[1], (int)arguments[2], list.Count(), -1);
		}
	}

	// operator LastIndexOf(const AList : list, const AValue : generic, const AStartIndex : Integer, const ALength : Integer);
	public class ListLastIndexOfStartLengthNode : BaseListIndexOfNode
	{
		public override object InternalExecute(Program program, object[] arguments)
		{
			#if NILPROPOGATION
			if (arguments[0] == null || arguments[1] == null || arguments[2] == null || arguments[3] == null)
				return null;
			#endif
			
			return InternalSearch(program, (ListValue)arguments[0], arguments[1], (int)arguments[2], (int)arguments[3], -1);
		}
	}

	// operator Remove(var AList : list, const AValue : generic);
	public class ListRemoveNode : BinaryInstructionNode
	{
		public PlanNode _equalNode; // The equality node used to compare each item in the list against AValue
		
		public override void DetermineDataType(Plan plan)
		{
			base.DetermineDataType(plan);
			plan.Symbols.Push(new Symbol("AValue", Nodes[1].DataType));
			try
			{
				plan.Symbols.Push(new Symbol("ACompareValue", ((Schema.ListType)Nodes[0].DataType).ElementType));
				try
				{
					_equalNode = Compiler.CompileExpression(plan, new BinaryExpression(new IdentifierExpression("AValue"), Instructions.Equal, new IdentifierExpression("ACompareValue")));
				}
				finally
				{
					plan.Symbols.Pop();
				}
			}
			finally
			{
				plan.Symbols.Pop();
			}
		}
		
		protected override void InternalBindingTraversal(Plan plan, PlanNodeVisitor visitor)
		{
			base.InternalBindingTraversal(plan, visitor);
			plan.Symbols.Push(new Symbol("AValue", Nodes[1].DataType));
			try
			{
				plan.Symbols.Push(new Symbol("ACompareValue", ((Schema.ListType)Nodes[0].DataType).ElementType));
				try
				{
					#if USEVISIT
					_equalNode = visitor.Visit(plan, _equalNode);
					#else
					_equalNode.BindingTraversal(plan, visitor);
					#endif
				}
				finally
				{
					plan.Symbols.Pop();
				}
			}
			finally
			{
				plan.Symbols.Pop();
			}
		}
		
		public override object InternalExecute(Program program, object argument1, object argument2)
		{
			ListValue list = (ListValue)argument1;
			int listIndex = -1;
			program.Stack.Push(argument2);
			try
			{
				for (int index = 0; index < list.Count(); index++)
				{
					program.Stack.Push(list[index]);
					try
					{
						object tempValue = _equalNode.Execute(program);
						if ((tempValue != null) && (bool)tempValue)
						{
							listIndex = index;
							break;
						}
					}
					finally
					{
						program.Stack.Pop();
					}
				}
			}
			finally
			{
				program.Stack.Pop();
			}

			((ListValue)argument1).RemoveAt(listIndex);
			return null;
		}

		protected override void InternalClone(PlanNode newNode)
		{
			base.InternalClone(newNode);

			if (_equalNode != null)
			{
				var newListRemoveNode = (ListRemoveNode)newNode;
				newListRemoveNode._equalNode = _equalNode.Clone();
			}
		}
	}
	
	// operator iEqual(const ALeftList : list, const ARightList : list) : Boolean;
	public class ListEqualNode : BinaryInstructionNode
	{
		public PlanNode _equalNode; // The equality node used to compare successive values in the lists
		
		public override void DetermineDataType(Plan plan)
		{
			DetermineModifiers(plan);
			plan.Symbols.Push(new Symbol("ALeftValue", ((Schema.ListType)Nodes[0].DataType).ElementType));
			try
			{
				plan.Symbols.Push(new Symbol("ARightValue", ((Schema.ListType)Nodes[1].DataType).ElementType));
				try
				{
					_equalNode = Compiler.CompileExpression(plan, new BinaryExpression(new IdentifierExpression("ALeftValue"), Instructions.Equal, new IdentifierExpression("ARightValue")));
				}
				finally
				{
					plan.Symbols.Pop();
				}
			}
			finally
			{
				plan.Symbols.Pop();
			}
		}
		
		protected override void InternalBindingTraversal(Plan plan, PlanNodeVisitor visitor)
		{
			base.InternalBindingTraversal(plan, visitor);
			plan.Symbols.Push(new Symbol("ALeftValue", ((Schema.ListType)Nodes[0].DataType).ElementType));
			try
			{
				plan.Symbols.Push(new Symbol("ARightValue", ((Schema.ListType)Nodes[1].DataType).ElementType));
				try
				{
					#if USEVISIT
					_equalNode = visitor.Visit(plan, _equalNode);
					#else
					_equalNode.BindingTraversal(plan, visitor);
					#endif
				}
				finally
				{
					plan.Symbols.Pop();
				}
			}
			finally
			{
				plan.Symbols.Pop();
			}
		}
		
		public override object InternalExecute(Program program, object argument1, object argument2)
		{
			ListValue leftList = (ListValue)argument1;
			ListValue rightList = (ListValue)argument2;
			#if NILPROPOGATION
			if ((leftList == null) || (rightList == null))
				return null;
			#endif
			
			bool listsEqual = leftList.Count() == rightList.Count();
			if (listsEqual)
			{
				for (int index = 0; index < leftList.Count(); index++)
				{
					program.Stack.Push(leftList[index]);
					try
					{
						program.Stack.Push(rightList[index]);
						try
						{
							object tempValue = _equalNode.Execute(program);
							#if NILPROPOGATION
							if ((tempValue == null))
								return null;
							#endif

							listsEqual = (bool)tempValue;
							if (!listsEqual)
								break;
						}
						finally
						{
							program.Stack.Pop();
						}
					}
					finally
					{
						program.Stack.Pop();
					}
				}
			}

			return listsEqual;
		}

		protected override void InternalClone(PlanNode newNode)
		{
			base.InternalClone(newNode);

			if (_equalNode != null)
			{
				var newListEqualNode = (ListEqualNode)newNode;
				newListEqualNode._equalNode = _equalNode.Clone();
			}
		}
	}

	// operator ToTable(const AList : list) : table
	// operator ToTable(const AList : list, const AColumnName : Name) : table
	// operator ToTable(const AList : list, const AColumnName : Name, const ASequenceName : Name) : table
	public class ListToTableNode : TableNode
	{
		public const string DefaultColumnName = "value";
		public const string DefaultSequenceName = "sequence";
		
		public override void DetermineDataType(Plan plan)
		{
			DetermineModifiers(plan);
			_dataType = new Schema.TableType();
			_tableVar = new Schema.ResultTableVar(this);
			_tableVar.Owner = plan.User;
			
			Schema.ListType listType = (Schema.ListType)Nodes[0].DataType;
			if (listType.ElementType is Schema.RowType)
			{
				// If given a list of rows, use the row's columns for the table
				foreach (Schema.Column column in ((Schema.RowType)listType.ElementType).Columns)
					DataType.Columns.Add(column.Copy());
			}
			else
			{
				// Determine the name for the value column
				string columnName = DefaultColumnName;
				if (Nodes.Count >= 2)
				{
					columnName = (string)plan.EvaluateLiteralArgument(Nodes[1], "AColumnName");
					SystemNameSelectorNode.CheckValidName(columnName);
				}
				
				DataType.Columns.Add(new Schema.Column(columnName, listType.ElementType));
			}

			// Determine the sequence column name
			string sequenceName = DefaultSequenceName;
			if (Nodes.Count == 3)
			{
				sequenceName = (string)plan.EvaluateLiteralArgument(Nodes[2], "ASequenceName");
				SystemNameSelectorNode.CheckValidName(sequenceName);
			}
			
			// Add sequence column and make it the key
			int sequencePrefix = 0;
			while (DataType.Columns.ContainsName(BuildName(sequenceName, sequencePrefix)))
				sequencePrefix++;
			Schema.Column sequenceColumn = new Schema.Column(BuildName(sequenceName, sequencePrefix), plan.DataTypes.SystemInteger);
			DataType.Columns.Add(sequenceColumn);
			_tableVar.EnsureTableVarColumns();
			_tableVar.Keys.Add(new Schema.Key(new Schema.TableVarColumn[] { _tableVar.Columns[sequenceColumn.Name] }));

			TableVar.DetermineRemotable(plan.CatalogDeviceSession);
			Order = Compiler.FindClusteringOrder(plan, TableVar);
			
			// Ensure the order exists in the orders list
			if (!TableVar.Orders.Contains(Order))
				TableVar.Orders.Add(Order);
		}

		private static string BuildName(string sequenceName, int sequencePrefix)
		{
			return sequenceName + (sequencePrefix == 0 ? "" : sequencePrefix.ToString());
		}
		
		public override object InternalExecute(Program program)
		{
			LocalTable result = new LocalTable(this, program);
			try
			{
				result.Open();
				
				using (ListValue listValue = Nodes[0].Execute(program) as ListValue)
				{
					if (listValue.DataType.ElementType is Schema.RowType)
					{
						for (int index = 0; index < listValue.Count(); index++)
						{
							Row row = new Row(program.ValueManager, DataType.RowType);
							(listValue[index] as Row).CopyTo(row);
							row[DataType.RowType.Columns.Count - 1] = index;
							result.Insert(row);
						}
					}
					else
					{
						for (int index = 0; index < listValue.Count(); index++)
						{
							Row row = new Row(program.ValueManager, DataType.RowType);
							row[0] = listValue[index];
							row[1] = index;
							result.Insert(row);
						}
					}
				}
				
				result.First();
				
				return result;
			}
			catch
			{
				result.Dispose();
				throw;
			}
		}
	}
	
	// operator ToList(const ATable : cursor) : list
	public class TableToListNode : InstructionNodeBase
	{
		public override void DetermineDataType(Plan plan)
		{
			_dataType = new Schema.ListType(((Schema.CursorType)Nodes[0].DataType).TableType.RowType);
		}
		
		protected bool CursorNext(Program program, Cursor cursor)
		{
			cursor.SwitchContext(program);
			try
			{
				return cursor.Table.Next();
			}
			finally
			{
				cursor.SwitchContext(program);
			}
		}
		
		protected Row CursorSelect(Program program, Cursor cursor)
		{
			cursor.SwitchContext(program);
			try
			{
				return cursor.Table.Select();
			}
			finally
			{
				cursor.SwitchContext(program);
			}
		}

		public override object InternalExecute(Program program)
		{
			Cursor cursor = program.CursorManager.GetCursor(((CursorValue)Nodes[0].Execute(program)).ID);
			try
			{
				ListValue listValue = new ListValue(program.ValueManager, (Schema.IListType)_dataType);
				while (CursorNext(program, cursor))
					listValue.Add(CursorSelect(program, cursor));

				return listValue;
			}
			finally
			{
				program.CursorManager.CloseCursor(cursor.ID);
			}
		}
	}
}
