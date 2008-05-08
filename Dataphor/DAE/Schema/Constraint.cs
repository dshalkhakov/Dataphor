/*
	Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/
using System;
using System.IO;
using System.Text;
using System.Collections;
using System.ComponentModel;
using System.Security.Permissions;
using System.Security.Cryptography;

using Alphora.Dataphor;
using Alphora.Dataphor.DAE;
using Alphora.Dataphor.DAE.Server;
using Alphora.Dataphor.DAE.Streams;
using Alphora.Dataphor.DAE.Language;
using Alphora.Dataphor.DAE.Language.D4;
using Alphora.Dataphor.DAE.Runtime;
using Alphora.Dataphor.DAE.Runtime.Data;
using Alphora.Dataphor.DAE.Runtime.Instructions;
using D4 = Alphora.Dataphor.DAE.Language.D4;

namespace Alphora.Dataphor.DAE.Schema
{
    public enum ConstraintType 
    { 
		/// <summary>A scalar type constraint is a truth valued expression which limits the set of values in a scalar type.</summary>
		ScalarType, 
		
		/// <summary>A Column constraint is a truth valued expression which limits the set of values on a column.  This constraint functions in addition to the type specification of the column, and is equivalent to a row constraint except that it is evaluable in terms of the column only.</summary>
		Column, 
		
		/// <summary>A Row constraint is a truth valued expression which limits the set of rows permissible in a table.</summary>
		Row, 
		
		/// <summary>A table constraint is a declarative construct which limits the set of rows permissible in a table such as a key.</summary>
		Table, 
		
		/// <summary>A database constraint is a truth valued expression which limits the set of table values permissible in the database.</summary>
		Database 
	}
	
	public enum Transition
	{
		Insert,
		Update,
		Delete
	}

	/// <remarks> Constraint </remarks>
	public abstract class Constraint : Object
    {
		// constructor
		public Constraint(string AName) : base(AName) {}
		public Constraint(int AID, string AName) : base(AID, AName) {}
		public Constraint(int AID, string AName, MetaData AMetaData) : base(AID, AName)
		{
			MetaData = AMetaData;
		}

		// ConstraintType
		private ConstraintType FConstraintType;
		public ConstraintType ConstraintType
		{
			get { return FConstraintType; }
			set { FConstraintType = value; }
		}
		
		// IsDeferred
		/// <summary>Indicates whether or not the constraint check should be deferred to transaction commit time.</summary>
		/// <remarks>
		/// Only database level constraints can be deferred.  By default all database level constraints are deferred.
		/// To change this behavior, use the DAE.IsDeferred tag.
		/// </remarks>
		public bool IsDeferred
		{
			get { return Boolean.Parse(MetaData.GetTag(MetaData, "DAE.IsDeferred", (ConstraintType == ConstraintType.Database).ToString())); }
			set
			{
				if (ConstraintType == ConstraintType.Database)
				{
					if (MetaData == null)
						MetaData = new MetaData();
					MetaData.Tags.AddOrUpdate("DAE.IsDeferred", value.ToString());
				}
			}
		}
		
		// Enforced
		private bool FEnforced = true;
		/// <summary>Indicates whether or not the constraint is enforced.</summary>
		/// <remarks>Set by the DAE.Enforced tag when the constraint is created.</remarks>
		public bool Enforced
		{
			get { return FEnforced; }
			set { FEnforced = value; }
		}
		
		public virtual string GetCustomMessage(Transition ATransition)
		{
			string LMessage = MetaData.GetTag(MetaData, "DAE.Message", String.Empty);
			if (LMessage == String.Empty)
			{
				LMessage = MetaData.GetTag(MetaData, "DAE.SimpleMessage", String.Empty);
				if (LMessage != String.Empty)
					LMessage = String.Format("\"{0}\"", LMessage);
			}
			return LMessage;
		}
		
		public string GetViolationMessage(ServerProcess AProcess, Transition ATransition)
		{
			return GetViolationMessage(AProcess, GetCustomMessage(ATransition), null, null);
		}
		
		public string GetViolationMessage(ServerProcess AProcess, Transition ATransition, Statement AConstraintDefinition)
		{
			return GetViolationMessage(AProcess, GetCustomMessage(ATransition), this, AConstraintDefinition);
		}
		
		/// <summary>Compiles or evaluates the custom violation message given in AMessage.</summary>
		/// <remarks>
		/// This method expects the stack to be prepared as appropriate for the type of constraint for which the message is being generated.
		/// If AConstraint is specified, AConstraintDefinition must be specified as well.
		/// If AConstraint is specified, then this call is a compile-time verification of the message syntax, and the message will not be evaluated,
		/// and any compile-time errors will be thrown out of this call.
		/// If AConstraint is not specified, this is the run-time evaluation of the message.
		/// </remarks>
		public static string GetViolationMessage(ServerProcess AProcess, string AMessage, Schema.Object AConstraint, Statement AConstraintDefinition)
		{
			if (AMessage != String.Empty)
			{
				try
				{
					ServerStatementPlan LPlan = new ServerStatementPlan(AProcess);
					try
					{
						Context LContext = AProcess.Context;
						if (AConstraint != null)
						{
							LPlan.Plan.PushCreationObject(AConstraint);
							LContext = AProcess.Plan.Symbols;
						}
						try
						{
							AProcess.PushExecutingPlan(LPlan);
							try
							{
								// Push current context onto the symbols for the new plan
								LPlan.Plan.EnterRowContext();
								DataVar LVar;
								for (int LIndex = LContext.Count - 1; LIndex >= 0; LIndex--)
								{
									LVar = LContext.Peek(LIndex);
									LPlan.Plan.Symbols.Push(new DataVar(LVar.Name == String.Empty ? Keywords.Value : LVar.Name, LVar.DataType));
								}
								
								// BTR 10/9/2006 ->
								// Setting this to true changes the binding to allow variable hiding. 
								// Without this, the error message expression evaluation will get an ambiguous resolve because of the state of the stack.
								LPlan.Plan.Symbols.AllowExtraWindowAccess = true; 

								#if USEISTRING
								PlanNode LExpressionNode = Compiler.CompileTypedExpression(LPlan.Plan, new D4.Parser().ParseExpression(AMessage), AProcess.DataTypes.SystemIString);
								#else
								PlanNode LExpressionNode = Compiler.CompileTypedExpression(LPlan.Plan, new D4.Parser().ParseExpression(AMessage), AProcess.DataTypes.SystemString);
								#endif
								LExpressionNode = Compiler.BindNode(LPlan.Plan, LExpressionNode);
								if (AConstraint == null)
									AMessage = LExpressionNode.Execute(AProcess).Value.AsString;
							}
							finally
							{
								AProcess.PopExecutingPlan(LPlan);
							}
						}
						finally
						{
							if (AConstraint != null)
								LPlan.Plan.PopCreationObject();
						}
					}
					finally
					{
						LPlan.Dispose();
					}
				}
				catch (Exception LException)
				{
					AMessage = String.Format("Errors occurred attempting to generate custom error message: {0}", LException.Message);
					if (AConstraint != null)
						throw new CompilerException(CompilerException.Codes.InvalidCustomConstraintMessage, AConstraintDefinition, LException, AConstraint.Name);
				}
			}
			
			if (AMessage != String.Empty)
			{
				if (AMessage[AMessage.Length - 1] != '.')
					AMessage = AMessage + '.';
			}

			return AMessage;
		}
		
		public abstract void Validate(ServerProcess AProcess, Transition ATransition);
    }
    
    public abstract class SimpleConstraint : Constraint
    {
		public SimpleConstraint(int AID, string AName) : base(AID, AName) {}
		
		// Expression
		private PlanNode FNode;
		public PlanNode Node
		{
			get { return FNode; }
			set { FNode = value; }
		}
    }

    public class ScalarTypeConstraint : SimpleConstraint
    {
		public ScalarTypeConstraint(int AID, string AName) : base(AID, AName) {}
		
		public override string Description { get { return String.Format(Strings.Get("SchemaObjectDescription.ScalarTypeConstraint"), DisplayName, FScalarType.DisplayName); } }

		public override bool IsPersistent { get { return true; } }

		internal ScalarType FScalarType;
		public ScalarType ScalarType
		{
			get { return FScalarType; }
			set
			{
				if (FScalarType != null)
					FScalarType.Constraints.Remove(this);
				if (value != null)
					value.Constraints.Add(this);
			}
		}

		public override int CatalogObjectID { get { return FScalarType == null ? -1 : FScalarType.ID; } }

		public override int ParentObjectID { get { return FScalarType == null ? -1 : FScalarType.ID; } }
		
		public ConstraintDefinition EmitDefinition(EmitMode AMode)
		{
			if (AMode == EmitMode.ForStorage)
			{
				SaveObjectID();
				SaveIsGenerated();
				SaveGeneratorID();
			}
			else
			{
				RemoveObjectID();
				RemoveIsGenerated();
				RemoveGeneratorID();
			}

			ConstraintDefinition LStatement = new ConstraintDefinition();
			LStatement.ConstraintName = Name;
			LStatement.MetaData = MetaData == null ? null : MetaData.Copy();
			LStatement.Expression = (Expression)Node.EmitStatement(AMode);
			return LStatement;
		}

		public override Statement EmitStatement(EmitMode AMode)
		{
			AlterScalarTypeStatement LStatement = new AlterScalarTypeStatement();
			LStatement.ScalarTypeName = Schema.Object.EnsureRooted(FScalarType.Name);
			LStatement.CreateConstraints.Add(EmitDefinition(AMode));
			return LStatement;
		}

		public override Statement EmitDropStatement(EmitMode AMode)
		{
			AlterScalarTypeStatement LStatement = new AlterScalarTypeStatement();
			LStatement.ScalarTypeName = Schema.Object.EnsureRooted(FScalarType.Name);
			LStatement.DropConstraints.Add(new DropConstraintDefinition(Name));
			return LStatement;
		}
		
		public override void Validate(ServerProcess AProcess, Transition ATransition)
		{
			DataVar LObject;
			try
			{
				LObject = Node.Execute(AProcess);
			}
			catch (Exception E)
			{
				throw new RuntimeException(RuntimeException.Codes.ErrorValidatingTypeConstraint, E, Name, FScalarType.Name);
			}
				
			if ((LObject.Value != null) && !LObject.Value.IsNil && !LObject.Value.AsBoolean)
			{
				string LMessage = GetViolationMessage(AProcess, ATransition);
				if (LMessage != String.Empty)
					throw new RuntimeException(RuntimeException.Codes.GeneralConstraintViolation, ErrorSeverity.User, LMessage);
				else
					throw new RuntimeException(RuntimeException.Codes.TypeConstraintViolation, ErrorSeverity.User, Name, FScalarType.Name);
			}
		}
    }
    
    public class TableVarColumnConstraint : SimpleConstraint
    {
		public TableVarColumnConstraint(int AID, string AName) : base(AID, AName) {}
		
		public override string Description { get { return String.Format(Strings.Get("SchemaObjectDescription.TableVarColumnConstraint"), DisplayName, FTableVarColumn.DisplayName, FTableVarColumn.TableVar.DisplayName); } }

		internal TableVarColumn FTableVarColumn;
		public TableVarColumn TableVarColumn
		{
			get { return FTableVarColumn; }
			set
			{
				if (FTableVarColumn != null)
					FTableVarColumn.Constraints.Remove(this);
				if (value != null)
					value.Constraints.Add(this);
			}
		}

		public override int CatalogObjectID { get { return FTableVarColumn == null ? -1 : FTableVarColumn.CatalogObjectID; } }

		public override int ParentObjectID { get { return FTableVarColumn == null ? -1 : FTableVarColumn.ID; } }
		
		public override bool IsATObject { get { return FTableVarColumn == null ? false : FTableVarColumn.IsATObject; } }
		
		public ConstraintDefinition EmitDefinition(EmitMode AMode)
		{
			if (AMode == EmitMode.ForStorage)
				SaveObjectID();
			else
				RemoveObjectID();

			ConstraintDefinition LStatement = new ConstraintDefinition();
			LStatement.ConstraintName = Name;
			LStatement.MetaData = MetaData == null ? null : MetaData.Copy();
			LStatement.Expression = (Expression)Node.EmitStatement(AMode);
			return LStatement;
		}

		public override Statement EmitStatement(EmitMode AMode)
		{	
			AlterTableStatement LStatement = new AlterTableStatement();
			LStatement.TableVarName = Schema.Object.EnsureRooted(FTableVarColumn.TableVar.Name);
			AlterColumnDefinition LDefinition = new AlterColumnDefinition();
			LDefinition.ColumnName = FTableVarColumn.Name;
			LDefinition.CreateConstraints.Add(EmitDefinition(AMode));
			LStatement.AlterColumns.Add(LDefinition);
			return LStatement;
		}

		public override Statement EmitDropStatement(EmitMode AMode)
		{
			if (FTableVarColumn.TableVar is BaseTableVar)
			{
				AlterTableStatement LStatement = new AlterTableStatement();
				LStatement.TableVarName = Schema.Object.EnsureRooted(FTableVarColumn.TableVar.Name);
				AlterColumnDefinition LDefinition = new D4.AlterColumnDefinition();
				LDefinition.ColumnName = FTableVarColumn.Name;
				LDefinition.DropConstraints.Add(new DropConstraintDefinition(Name));
				LStatement.AlterColumns.Add(LDefinition);
				return LStatement;
			}
			else
				return new Block();
		}

		public override void Validate(ServerProcess AProcess, Transition ATransition)
		{
			DataVar LObject;
			try
			{
				LObject = Node.Execute(AProcess);
			}
			catch (Exception E)
			{
				throw new RuntimeException(RuntimeException.Codes.ErrorValidatingColumnConstraint, E, Name, TableVarColumn.Name, TableVarColumn.TableVar.DisplayName);
			}
			
			if ((LObject.Value != null) && !LObject.Value.IsNil && !LObject.Value.AsBoolean)
			{
				string LMessage = GetViolationMessage(AProcess, ATransition);
				if (LMessage != String.Empty)
					throw new RuntimeException(RuntimeException.Codes.GeneralConstraintViolation, ErrorSeverity.User, LMessage);
				else
					throw new RuntimeException(RuntimeException.Codes.ColumnConstraintViolation, ErrorSeverity.User, Name, TableVarColumn.Name, TableVarColumn.TableVar.DisplayName);
			}
		}
    }
    
    public abstract class TableVarConstraint : Constraint
    {
		public TableVarConstraint(string AName) : base(AName) {}
		public TableVarConstraint(int AID, string AName) : base(AID, AName) {}
		
		internal TableVar FTableVar;
		public TableVar TableVar
		{
			get { return FTableVar; }
			set
			{
				if (FTableVar != null)
					FTableVar.Constraints.Remove(this);
				if (value != null)
					value.Constraints.Add(this);
			}
		}

		/// <summary>Table var constraints are always persistent.</summary>
		public override bool IsPersistent { get { return true; } }

		public override int CatalogObjectID { get { return FTableVar == null ? -1 : FTableVar.ID; } }

		public override int ParentObjectID { get { return FTableVar == null ? -1 : FTableVar.ID; } }
		
		public override bool IsATObject { get { return FTableVar == null ? false : FTableVar.IsATObject; } }
		
		public abstract Statement EmitDefinition(EmitMode AMode);

		public override Statement EmitStatement(EmitMode AMode)
		{
			if (AMode == EmitMode.ForStorage)
				SaveObjectID();
			else
				RemoveObjectID();

			AlterTableVarStatement LStatement = (TableVar is BaseTableVar) ? (AlterTableVarStatement)new AlterTableStatement() : (AlterTableVarStatement)new AlterViewStatement();
			LStatement.TableVarName = TableVar.Name;
			LStatement.CreateConstraints.Add(EmitDefinition(AMode));
			return LStatement;
		}

		/// <summary>Returns whether or not the constraint needs to be validated for the specified transition given the specified value flags.</summary>
		public abstract bool ShouldValidate(BitArray AValueFlags, Transition ATransition);
	}
    
    public class RowConstraint : TableVarConstraint
    {
		public RowConstraint(int AID, string AName) : base(AID, AName) {}
		
		public override string Description { get { return String.Format(Strings.Get("SchemaObjectDescription.RowConstraint"), DisplayName, TableVar.DisplayName); } }

		// Node
		private PlanNode FNode;
		public PlanNode Node
		{
			get { return FNode; }
			set { FNode = value; }
		}
		
		// ColumnFlags
		private BitArray FColumnFlags;
		/// <summary>If specified, indicates which columns are referenced by the constraint</summary>
		public BitArray ColumnFlags
		{
			get { return FColumnFlags; }
			set { FColumnFlags = value; }
		}
		
		public override Statement EmitDefinition(EmitMode AMode)
		{
			ConstraintDefinition LStatement = new ConstraintDefinition();
			LStatement.ConstraintName = Name;
			LStatement.MetaData = MetaData == null ? null : MetaData.Copy();
			LStatement.Expression = (Expression)Node.EmitStatement(AMode);
			return LStatement;
		}
		
		public override Statement EmitDropStatement(EmitMode AMode)
		{
			AlterTableVarStatement LStatement = FTableVar is Schema.BaseTableVar ? (AlterTableVarStatement)new AlterTableStatement() : new AlterViewStatement();
			LStatement.TableVarName = Schema.Object.EnsureRooted(FTableVar.Name);
			DropConstraintDefinition LDefinition = new DropConstraintDefinition(Name);
			LStatement.DropConstraints.Add(LDefinition);
			return LStatement;
		}
		
		/// <summary>Returns whether or not the constraint needs to be validated given the specified value flags.</summary>
		public override bool ShouldValidate(BitArray AValueFlags, Schema.Transition ATransition)
		{
			if ((FColumnFlags != null) && (AValueFlags != null))
			{
				for (int LIndex = 0; LIndex < FColumnFlags.Length; LIndex++)
					if (FColumnFlags[LIndex] && AValueFlags[LIndex])
						return true;
				return false;
			}

			return true;
		}
		
		public override void Validate(ServerProcess AProcess, Transition ATransition)
		{
			DataVar LObject;
			try
			{
				LObject = Node.Execute(AProcess);
			}
			catch (Exception E)
			{
				throw new RuntimeException(RuntimeException.Codes.ErrorValidatingRowConstraint, E, Name, TableVar.DisplayName);
			}

			if ((LObject.Value != null) && !LObject.Value.IsNil && !LObject.Value.AsBoolean)
			{
				string LMessage = GetViolationMessage(AProcess, ATransition);
				if (LMessage != String.Empty)
					throw new RuntimeException(RuntimeException.Codes.GeneralConstraintViolation, ErrorSeverity.User, LMessage);
				else
					throw new RuntimeException(RuntimeException.Codes.RowConstraintViolation, ErrorSeverity.User, Name, TableVar.DisplayName);
			}
		}
	}

    /// <remarks> RowConstraints </remarks>
	public class RowConstraints : Objects
    {		
		#if USEOBJECTVALIDATE
		protected override void Validate(Object AItem)
		{
			if (!(AItem is RowConstraint))
				throw new SchemaException(SchemaException.Codes.InvalidContainer, "RowConstraint");
			base.Validate(AItem);
		}
		#endif

		public new RowConstraint this[int AIndex]
		{
			get { return (RowConstraint)base[AIndex]; }
			set { base[AIndex] = value; }
		}

		public new RowConstraint this[string AName]
		{
			get { return (RowConstraint)base[AName]; }
			set { base[AName] = value; }
		}
    }
    
    public class TransitionConstraint : TableVarConstraint
    {
		public TransitionConstraint(string AName) : base(AName) {}
		public TransitionConstraint(int AID, string AName) : base(AID, AName) {}
		
		public override string Description { get { return String.Format(Strings.Get("SchemaObjectDescription.TransitionConstraint"), DisplayName, TableVar.DisplayName); } }

		// OnInsertNode
		private PlanNode FOnInsertNode;
		public PlanNode OnInsertNode
		{
			get { return FOnInsertNode; }
			set { FOnInsertNode = value; }
		}
		
		// InsertColumnFlags
		private BitArray FInsertColumnFlags;
		/// <summary>If specified, indicates which columns are referenced by the insert constraint</summary>
		public BitArray InsertColumnFlags
		{
			get { return FInsertColumnFlags; }
			set { FInsertColumnFlags = value; }
		}
		
		// OnUpdateNode
		private PlanNode FOnUpdateNode;
		public PlanNode OnUpdateNode
		{
			get { return FOnUpdateNode; }
			set { FOnUpdateNode = value; }
		}
		
		// UpdateColumnFlags
		private BitArray FUpdateColumnFlags;
		/// <summary>If specified, indicates which columns are referenced by the update constraint</summary>
		public BitArray UpdateColumnFlags
		{
			get { return FUpdateColumnFlags; }
			set { FUpdateColumnFlags = value; }
		}
		
		// OnDeleteNode
		private PlanNode FOnDeleteNode;
		public PlanNode OnDeleteNode
		{
			get { return FOnDeleteNode; }
			set { FOnDeleteNode = value; }
		}
		
		// DeleteColumnFlags
		private BitArray FDeleteColumnFlags;
		/// <summary>If specified, indicates which columns are referenced by the delete constraint</summary>
		public BitArray DeleteColumnFlags
		{
			get { return FDeleteColumnFlags; }
			set { FDeleteColumnFlags = value; }
		}
		
		public override Statement EmitDefinition(EmitMode AMode)
		{
			TransitionConstraintDefinition LStatement = new TransitionConstraintDefinition();
			LStatement.ConstraintName = Name;
			LStatement.MetaData = MetaData == null ? null : MetaData.Copy();
			if (FOnInsertNode != null)
				LStatement.OnInsertExpression = (Expression)FOnInsertNode.EmitStatement(AMode);
			if (FOnUpdateNode != null)
				LStatement.OnUpdateExpression = (Expression)FOnUpdateNode.EmitStatement(AMode);
			if (FOnDeleteNode != null)
				LStatement.OnDeleteExpression = (Expression)FOnDeleteNode.EmitStatement(AMode);
			return LStatement;
		}
		
		public override Statement EmitDropStatement(EmitMode AMode)
		{
			AlterTableVarStatement LStatement = FTableVar is Schema.BaseTableVar ? (AlterTableVarStatement)new AlterTableStatement() : new AlterViewStatement();
			LStatement.TableVarName = Schema.Object.EnsureRooted(FTableVar.Name);
			DropConstraintDefinition LDefinition = new DropConstraintDefinition(Name);
			LDefinition.IsTransition = true;
			LStatement.DropConstraints.Add(LDefinition);
			return LStatement;
		}
		
		public override string GetCustomMessage(Transition ATransition)
		{
			string LMessage = MetaData.GetTag(MetaData, String.Format("DAE.{0}.Message", ATransition.ToString()), MetaData.GetTag(MetaData, "DAE.Message", String.Empty));
			if (LMessage == String.Empty)
			{
				LMessage = MetaData.GetTag(MetaData, String.Format("DAE.{0}.SimpleMessage", ATransition.ToString()), MetaData.GetTag(MetaData, "DAE.SimpleMessage", String.Empty));
				if (LMessage != String.Empty)
					LMessage = String.Format("\"{0}\"", LMessage);
			}
			return LMessage;
		}

		/// <summary>Returns whether or not the constraint needs to be validated given the specified value flags.</summary>
		public override bool ShouldValidate(BitArray AValueFlags, Schema.Transition ATransition)
		{
			switch (ATransition)
			{
				case Transition.Insert :
					if ((FInsertColumnFlags != null) && (AValueFlags != null))
					{
						for (int LIndex = 0; LIndex < FInsertColumnFlags.Length; LIndex++)
							if (FInsertColumnFlags[LIndex] && AValueFlags[LIndex])
								return true;
						return false;
					}
					return true;
				
				case Transition.Update :
					if ((FUpdateColumnFlags != null) && (AValueFlags != null))
					{
						for (int LIndex = 0; LIndex < FUpdateColumnFlags.Length; LIndex++)
							if (FUpdateColumnFlags[LIndex] && AValueFlags[LIndex])
								return true;
						return false;
					}
					return true;
				
				case Transition.Delete :
					if ((FDeleteColumnFlags != null) && (AValueFlags != null))
					{
						for (int LIndex = 0; LIndex < FDeleteColumnFlags.Length; LIndex++)
							if (FDeleteColumnFlags[LIndex] && AValueFlags[LIndex])
								return true;
						return false;
					}
					return true;
				
				default : return true;
			}
		}

		public override void Validate(ServerProcess AProcess, Transition ATransition)
		{
			DataVar LObject;
			switch (ATransition)
			{
				case Transition.Insert :
					try
					{
						LObject = OnInsertNode.Execute(AProcess);
					}
					catch (Exception E)
					{
						throw new RuntimeException(RuntimeException.Codes.ErrorValidatingInsertConstraint, E, Name, TableVar.DisplayName);
					}
					
					if ((LObject.Value != null) && !LObject.Value.IsNil && !LObject.Value.AsBoolean)
					{
						string LMessage = GetViolationMessage(AProcess, ATransition);
						if (LMessage != String.Empty)
							throw new RuntimeException(RuntimeException.Codes.GeneralConstraintViolation, ErrorSeverity.User, LMessage);
						else
							throw new RuntimeException(RuntimeException.Codes.InsertConstraintViolation, ErrorSeverity.User, Name, TableVar.DisplayName);
					}
				break;
				
				case Transition.Update :
					try
					{
						LObject = OnUpdateNode.Execute(AProcess);
					}
					catch (Exception E)
					{
						throw new RuntimeException(RuntimeException.Codes.ErrorValidatingUpdateConstraint, E, Name, TableVar.DisplayName);
					}

					if ((LObject.Value != null) && !LObject.Value.IsNil && !LObject.Value.AsBoolean)
					{
						string LMessage = GetViolationMessage(AProcess, ATransition);
						if (LMessage != String.Empty)
							throw new RuntimeException(RuntimeException.Codes.GeneralConstraintViolation, ErrorSeverity.User, LMessage);
						else
							throw new RuntimeException(RuntimeException.Codes.UpdateConstraintViolation, ErrorSeverity.User, Name, TableVar.DisplayName);
					}
				break;
				
				case Transition.Delete :
					try
					{
						LObject = OnDeleteNode.Execute(AProcess);
					}
					catch (Exception E)
					{
						throw new RuntimeException(RuntimeException.Codes.ErrorValidatingDeleteConstraint, E, Name, TableVar.DisplayName);
					}
					
					if ((LObject.Value != null) && !LObject.Value.IsNil && !LObject.Value.AsBoolean)
					{
						string LMessage = GetViolationMessage(AProcess, ATransition);
						if (LMessage != String.Empty)
							throw new RuntimeException(RuntimeException.Codes.GeneralConstraintViolation, ErrorSeverity.User, LMessage);
						else
							throw new RuntimeException(RuntimeException.Codes.DeleteConstraintViolation, ErrorSeverity.User, Name, TableVar.DisplayName);
					}
				break;
			}
		}
    }
    
    /// <remarks> TransitionConstraints </remarks>
	public class TransitionConstraints : Objects
    {		
		#if USEOBJECTVALIDATE
		protected override void Validate(Object AItem)
		{
			if (!(AItem is TransitionConstraint))
				throw new SchemaException(SchemaException.Codes.InvalidContainer, "TransitionConstraint");
			base.Validate(AItem);
		}
		#endif

		public new TransitionConstraint this[int AIndex]
		{
			get { return (TransitionConstraint)base[AIndex]; }
			set { base[AIndex] = value; }
		}

		public new TransitionConstraint this[string AName]
		{
			get { return (TransitionConstraint)base[AName]; }
			set { base[AName] = value; }
		}
    }

    /// <remarks> Constraints </remarks>
	public class Constraints : Objects
    {		
		#if USEOBJECTVALIDATE
		protected override void Validate(Object AItem)
		{
			if (!(AItem is Constraint))
				throw new SchemaException(SchemaException.Codes.ConstraintContainer);
			base.Validate(AItem);
		}
		#endif

		public new Constraint this[int AIndex]
		{
			get { return (Constraint)base[AIndex]; }
			set { base[AIndex] = value; }
		}

		public new Constraint this[string AName]
		{
			get { return (Constraint)base[AName]; }
			set { base[AName] = value; }
		}
    }
    
    public class ScalarTypeConstraints : Objects
    {
		public ScalarTypeConstraints(ScalarType AScalarType) : base()
		{
			FScalarType = AScalarType;
		}
		
		private ScalarType FScalarType;
		public ScalarType ScalarType { get { return FScalarType; } }
		
		#if USEOBJECTVALIDATE
		protected override void Validate(Object AItem)
		{
			if (!(AItem is ScalarTypeConstraint))
				throw new SchemaException(SchemaException.Codes.InvalidContainer, "ScalarTypeConstraint");
			base.Validate(AItem);
		}
		#endif
		
		protected override void Adding(Object AItem, int AIndex)
		{
			base.Adding(AItem, AIndex);
			((ScalarTypeConstraint)AItem).FScalarType = FScalarType;
		}
		
		protected override void Removing(Object AItem, int AIndex)
		{
			((ScalarTypeConstraint)AItem).FScalarType = null;
			base.Removing(AItem, AIndex);
		}
		
		public new ScalarTypeConstraint this[int AIndex]
		{
			get { return (ScalarTypeConstraint)base[AIndex]; }
			set { base[AIndex] = value; }
		}
		
		public new ScalarTypeConstraint this[string AName]
		{
			get { return (ScalarTypeConstraint)base[AName]; }
			set { base[AName] = value; }
		}
    }

    public class TableVarColumnConstraints : Objects
    {
		public TableVarColumnConstraints(TableVarColumn ATableVarColumn) : base()
		{
			FTableVarColumn = ATableVarColumn;
		}
		
		private TableVarColumn FTableVarColumn;
		public TableVarColumn TableVarColumn { get { return FTableVarColumn; } }
		
		#if USEOBJECTVALIDATE
		protected override void Validate(Object AItem)
		{
			if (!(AItem is TableVarColumnConstraint))
				throw new SchemaException(SchemaException.Codes.InvalidContainer, "TableVarColumnConstraint");
			base.Validate(AItem);
		}
		#endif
		
		protected override void Adding(Object AItem, int AIndex)
		{
			base.Adding(AItem, AIndex);
			((TableVarColumnConstraint)AItem).FTableVarColumn = FTableVarColumn;
		}
		
		protected override void Removing(Object AItem, int AIndex)
		{
			((TableVarColumnConstraint)AItem).FTableVarColumn = null;
			base.Removing(AItem, AIndex);
		}
		
		public new TableVarColumnConstraint this[int AIndex]
		{
			get { return (TableVarColumnConstraint)base[AIndex]; }
			set { base[AIndex] = value; }
		}
		
		public new TableVarColumnConstraint this[string AName]
		{
			get { return (TableVarColumnConstraint)base[AName]; }
			set { base[AName] = value; }
		}
    }

    public class TableVarConstraints : Objects
    {
		public TableVarConstraints(TableVar ATableVar) : base()
		{
			FTableVar = ATableVar;
		}
		
		private TableVar FTableVar;
		public TableVar TableVar { get { return FTableVar; } }
		
		#if USEOBJECTVALIDATE
		protected override void Validate(Object AItem)
		{
			if (!(AItem is TableVarConstraint))
				throw new SchemaException(SchemaException.Codes.InvalidContainer, "TableVarConstraint");
			base.Validate(AItem);
		}
		#endif
		
		protected override void Adding(Object AItem, int AIndex)
		{
			base.Adding(AItem, AIndex);
			((TableVarConstraint)AItem).FTableVar = FTableVar;
		}
		
		protected override void Removing(Object AItem, int AIndex)
		{
			((TableVarConstraint)AItem).FTableVar = null;
			base.Removing(AItem, AIndex);
		}
		
		public new TableVarConstraint this[int AIndex]
		{
			get { return (TableVarConstraint)base[AIndex]; }
			set { base[AIndex] = value; }
		}
		
		public new TableVarConstraint this[string AName]
		{
			get { return (TableVarConstraint)base[AName]; }
			set { base[AName] = value; }
		}
    }

    public class CatalogConstraint : CatalogObject
    {
		// constructor
		public CatalogConstraint(string AName) : base(AName) {}
		public CatalogConstraint(int AID, string AName) : base(AID, AName) {}
		public CatalogConstraint(int AID, string AName, PlanNode ANode) : base(AID, AName)
		{
			FNode = ANode;
		}
		
		public CatalogConstraint(int AID, string AName, MetaData AMetaData, PlanNode ANode) : base(AID, AName)
		{
			MetaData = AMetaData;
			FNode = ANode;
		}

		public override string Description { get { return String.Format(Strings.Get("SchemaObjectDescription.CatalogConstraint"), DisplayName); } }

		// Expression
		private PlanNode FNode;
		public PlanNode Node
		{
			get { return FNode; }
			set { FNode = value; }
		}
		
		// ConstraintType
		public ConstraintType ConstraintType
		{
			get { return ConstraintType.Database; }
			set { }
		}

		// Enforced
		private bool FEnforced = true;
		/// <summary>Indicates whether or not the constraint is enforced.</summary>
		/// <remarks>Set by the DAE.Enforced tag when the constraint is created.</remarks>
		public bool Enforced
		{
			get { return FEnforced; }
			set { FEnforced = value; }
		}
		
		// IsDeferred
		/// <summary>Indicates whether or not the constraint check should be deferred to transaction commit time.</summary>
		/// <remarks>
		/// Only database level constraints can be deferred.  By default all database level constraints are deferred.
		/// To change this behavior, use the DAE.IsDeferred tag.
		/// </remarks>
		public bool IsDeferred
		{
			get { return Boolean.Parse(MetaData.GetTag(MetaData, "DAE.IsDeferred", (ConstraintType == ConstraintType.Database).ToString())); }
			set
			{
				if (ConstraintType == ConstraintType.Database)
				{
					if (MetaData == null)
						MetaData = new MetaData();
					MetaData.Tags.AddOrUpdate("DAE.IsDeferred", value.ToString());
				}
			}
		}
		
		public override Statement EmitStatement(EmitMode AMode)
		{
			if (AMode == EmitMode.ForStorage)
				SaveObjectID();
			else
				RemoveObjectID();

			CreateConstraintStatement LStatement = new CreateConstraintStatement();
			if (SessionObjectName != null)
			{
				LStatement.IsSession = true;
				LStatement.ConstraintName = Schema.Object.EnsureRooted(SessionObjectName);
			}
			else
				LStatement.ConstraintName = Schema.Object.EnsureRooted(Name);
			LStatement.MetaData = MetaData == null ? new MetaData() : MetaData.Copy();
			if (SessionObjectName != null)
				LStatement.MetaData.Tags.AddOrUpdate("DAE.GlobalObjectName", Name, true);
			LStatement.Expression = (Expression)Node.EmitStatement(AMode);
			return LStatement;
		}
		
		public override Statement EmitDropStatement(EmitMode AMode)
		{
			DropConstraintStatement LStatement = new DropConstraintStatement();
			LStatement.ConstraintName = Schema.Object.EnsureRooted(Name);
			return LStatement;
		}

		public override string[] GetRights()
		{
			return new string[]
			{
				Name + Schema.RightNames.Alter,
				Name + Schema.RightNames.Drop
			};
		}
		
		public string GetCustomMessage()
		{
			string LMessage = MetaData.GetTag(MetaData, "DAE.Message", String.Empty);
			if (LMessage == String.Empty)
			{
				LMessage = MetaData.GetTag(MetaData, "DAE.SimpleMessage", String.Empty);
				if (LMessage != String.Empty)
					LMessage = String.Format("\"{0}\"", LMessage);
			}
			return LMessage;
		}
		
		public string GetViolationMessage(ServerProcess AProcess, Statement AConstraintDefinition)
		{
			return Schema.Constraint.GetViolationMessage(AProcess, GetCustomMessage(), this, AConstraintDefinition);
		}
		
		public string GetViolationMessage(ServerProcess AProcess)
		{
			return Schema.Constraint.GetViolationMessage(AProcess, GetCustomMessage(), null, null);
		}

		public void Validate(ServerProcess AProcess)
		{
			DataVar LObject;
			
			try
			{
				LObject = Node.Execute(AProcess);
			}
			catch (Exception E)
			{
				throw new RuntimeException(RuntimeException.Codes.ErrorValidatingCatalogConstraint, E, Name);
			}
			
			if ((LObject.Value != null) && !LObject.Value.IsNil && !LObject.Value.AsBoolean)
			{
				string LMessage = GetViolationMessage(AProcess);
				if (LMessage != String.Empty)
					throw new RuntimeException(RuntimeException.Codes.GeneralConstraintViolation, ErrorSeverity.User, LMessage);
				else
					throw new RuntimeException(RuntimeException.Codes.CatalogConstraintViolation, ErrorSeverity.User, DisplayName);
			}
		}		
    }
    
    /// <remarks> CatalogConstraints </remarks>
	public class CatalogConstraints : Objects
    {		
		#if USEOBJECTVALIDATE
		protected override void Validate(Object AItem)
		{
			if (!(AItem is CatalogConstraint))
				throw new SchemaException(SchemaException.Codes.InvalidContainer, "CatalogConstraint");
			base.Validate(AItem);
		}
		#endif

		public new CatalogConstraint this[int AIndex]
		{
			get { return (CatalogConstraint)base[AIndex]; }
			set { base[AIndex] = value; }
		}

		public new CatalogConstraint this[string AName]
		{
			get { return (CatalogConstraint)base[AName]; }
			set { base[AName] = value; }
		}
    }
}