/*
	Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/

using System;
using System.IO;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Security.Permissions;
using System.Security.Cryptography;

namespace Alphora.Dataphor.DAE.Schema
{
	using Alphora.Dataphor.DAE.Language;
	using Alphora.Dataphor.DAE.Language.D4;
	using Alphora.Dataphor.DAE.Device.Catalog;

	// TODO: Need to refactor these dependencies
	//using Alphora.Dataphor.DAE.Compiling;
	//using Alphora.Dataphor.DAE.Server;
	//using Alphora.Dataphor.DAE.Streams;
	//using Alphora.Dataphor.DAE.Runtime;
	using Alphora.Dataphor.DAE.Runtime.Data; // Need to move NativeRepresentation resolution to the ValueManager
	using Alphora.Dataphor.DAE.Runtime.Instructions; // PlanNode

	public class Property : Object
	{
		public Property(int iD, string name) : base(iD, name) {}
		
		public Property(int iD, string name, IDataType dataType) : base(name)
		{
			_dataType = dataType;
		}
		
		public override string Description { get { return String.Format(Strings.Get("SchemaObjectDescription.Property"), DisplayName, _representation.DisplayName, _representation.ScalarType.DisplayName); } }

		public override int CatalogObjectID { get { return _representation == null ? -1 : _representation.CatalogObjectID; } }

		public override int ParentObjectID { get { return _representation == null ? -1 : _representation.ID; } }

		[Reference]
		internal Representation _representation;
		public Representation Representation
		{
			get { return _representation; }
			set
			{
				if (_representation != null)
					_representation.Properties.Remove(this);
				if (value != null)
					value.Properties.Add(this);
			}
		}
		
		[Reference]
		private IDataType _dataType;
		public IDataType DataType
		{
			get { return _dataType; }
			set { _dataType = value; }
		}

		private bool _isDefaultReadAccessor;
		public bool IsDefaultReadAccessor
		{
			get { return _isDefaultReadAccessor; }
			set { _isDefaultReadAccessor = value; }
		}
		
		private int _readAccessorID = -1;
		public int ReadAccessorID
		{
			get { return _readAccessorID; }
			set { _readAccessorID = value; }
		}

		public void LoadReadAccessorID()
		{
			Tag tag = MetaData.RemoveTag(MetaData, "DAE.ReadAccessorID");
			if (tag != Tag.None)
				_readAccessorID = Int32.Parse(tag.Value);
		}
		
		public void SaveReadAccessorID()
		{
			MetaData.Tags.AddOrUpdate("DAE.ReadAccessorID", _readAccessorID.ToString(), true);
		}
		
		public void RemoveReadAccessorID()
		{
			if (MetaData != null)
				MetaData.Tags.RemoveTag("DAE.ReadAccessorID");
		}

		public void ResolveReadAccessor(CatalogDeviceSession session)
		{
			if ((_readAccessor == null) && (_readAccessorID > -1))
				_readAccessor = session.ResolveCatalogObject(_readAccessorID) as Schema.Operator;
		}

        // ReadAccessor
		[Reference]
        private Operator _readAccessor;
        public Operator ReadAccessor
        {
			get { return _readAccessor; }
			set 
			{ 
				_readAccessor = value; 
				_readAccessorID = value == null ? -1 : value.ID;
			}
        }
        
		private bool _isDefaultWriteAccessor;
		public bool IsDefaultWriteAccessor
		{
			get { return _isDefaultWriteAccessor; }
			set { _isDefaultWriteAccessor = value; }
		}
		
		private int _writeAccessorID = -1;
		public int WriteAccessorID
		{
			get { return _writeAccessorID; }
			set { _writeAccessorID = value; }
		}

		public void LoadWriteAccessorID()
		{
			Tag tag = MetaData.RemoveTag(MetaData, "DAE.WriteAccessorID");
			if (tag != Tag.None)
				_writeAccessorID = Int32.Parse(tag.Value);
		}
		
		public void SaveWriteAccessorID()
		{
			MetaData.Tags.AddOrUpdate("DAE.WriteAccessorID", _writeAccessorID.ToString(), true);
		}
		
		public void RemoveWriteAccessorID()
		{
			if (MetaData != null)
				MetaData.Tags.RemoveTag("DAE.WriteAccessorID");
		}

		public void ResolveWriteAccessor(CatalogDeviceSession session)
		{
			if ((_writeAccessor == null) && (_writeAccessorID > -1))
				_writeAccessor = session.ResolveCatalogObject(_writeAccessorID) as Schema.Operator;
		}

        // WriteAccessor
		[Reference]
        private Operator _writeAccessor;
        public Operator WriteAccessor
        {
			get { return _writeAccessor; }
			set 
			{ 
				_writeAccessor = value; 
				_writeAccessorID = value == null ? -1 : value.ID;
			}
        }

		public override Statement EmitStatement(EmitMode mode)
		{
			if (mode == EmitMode.ForStorage)
			{
				SaveObjectID();
				SaveReadAccessorID();
				SaveWriteAccessorID();
			}
			else
			{
				RemoveObjectID();
				RemoveReadAccessorID();
				RemoveWriteAccessorID();
			}
			
			PropertyDefinition property = new PropertyDefinition(Name, DataType.EmitSpecifier(mode));
			if (!_isDefaultReadAccessor)
				property.ReadAccessorBlock = _readAccessor.Block.EmitAccessorBlock(mode);
			
			if (!_isDefaultWriteAccessor)
				property.WriteAccessorBlock = _writeAccessor.Block.EmitAccessorBlock(mode);
			
			property.MetaData = MetaData == null ? null : MetaData.Copy();
			return property;
		}

		public override void IncludeDependencies(CatalogDeviceSession session, Catalog sourceCatalog, Catalog targetCatalog, EmitMode mode)
		{
			base.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
			
			ReadAccessor.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
			WriteAccessor.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
		}

		public bool HasExternalDependencies(Schema.ScalarType LSystemType)
		{
			if (HasDependencies())
			{
				if ((Representation != null) && (Representation.ScalarType != null) && ((Representation.ScalarType.LikeType != null) || (LSystemType != null)))
				{
					for (int index = 0; index < Dependencies.Count; index++)
						if 
						(
							((Representation.ScalarType.LikeType != null) && (Dependencies.IDs[index] != Representation.ScalarType.LikeType.ID)) || 
							((LSystemType != null) && (Dependencies.IDs[index] != LSystemType.ID))
						)
							return true;
				}
				else
					return true;
			}
			
			return false;
		}
	}
	
    /// <remarks> Properties </remarks>
	public class Properties : Objects
    {
		public Properties(Representation representation) : base()
		{
			_representation = representation;
		}
		
		[Reference]
		private Representation _representation;
		public Representation Representation { get { return _representation; } }
		
		#if USEOBJECTVALIDATE
		protected override void Validate(Object item)
		{
			if (!(item is Property))
				throw new SchemaException(SchemaException.Codes.PropertyContainer);
			base.Validate(item);
		}
		#endif
		
		protected override void Adding(Object item, int index)
		{
			base.Adding(item, index);
			((Property)item)._representation = _representation;
		}
		
		protected override void Removing(Object item, int index)
		{
			((Property)item)._representation = null;
			base.Removing(item, index);
		}

		public new Property this[int index]
		{
			get { return (Property)base[index]; }
			set { base[index] = value; }
		}

		public new Property this[string name]
		{
			get { return (Property)base[name]; }
			set { base[name] = value; }
		}
    }

	public class Representation : Object
	{
		public Representation(int iD, string name) : base(iD, name) 
		{
			_properties = new Properties(this);
		}
		
		public override string Description { get { return String.Format(Strings.Get("SchemaObjectDescription.Representation"), DisplayName, _scalarType.DisplayName); } }

		public override int CatalogObjectID { get { return _scalarType == null ? -1 : _scalarType.ID; } }

 		public override int ParentObjectID { get { return _scalarType == null ? -1 : _scalarType.ID; } }

		[Reference]
		internal ScalarType _scalarType;
		public ScalarType ScalarType
		{
			get { return _scalarType; }
			set
			{
				if (_scalarType != null)
					_scalarType.Representations.Remove(this);
				if (value != null)
					value.Representations.Add(this);
			}
		}
		
		private bool _isDefaultSelector;
		public bool IsDefaultSelector
		{
			get { return _isDefaultSelector; }
			set { _isDefaultSelector = value; }
		}
		
		private int _selectorID = -1;
		public int SelectorID
		{
			get { return _selectorID; }
			set { _selectorID = value; }
		}
		
		public void LoadSelectorID()
		{
			Tag tag = MetaData.RemoveTag(MetaData, "DAE.SelectorID");
			if (tag != Tag.None)
				_selectorID = Int32.Parse(tag.Value);
		}

		public void SaveSelectorID()
		{
			MetaData.Tags.AddOrUpdate("DAE.SelectorID", _selectorID.ToString(), true);
		}
		
		public void RemoveSelectorID()
		{
			if (MetaData != null)
				MetaData.Tags.RemoveTag("DAE.SelectorID");
		}

		public void ResolveSelector(CatalogDeviceSession session)
		{
			if ((_selector == null) && (_selectorID > -1))
				_selector = session.ResolveCatalogObject(_selectorID) as Schema.Operator;
		}

        // Selector -- the selector operator for this representation
		[Reference]
        private Operator _selector;
        public Operator Selector
        {
			get { return _selector; }
			set 
			{ 
				_selector = value; 
				_selectorID = value == null ? -1 : value.ID;
			}
        }

		// Properties
		private Properties _properties;
		public Properties Properties { get { return _properties; } } 
		
		public RepresentationDefinition EmitDefinition(EmitMode mode)
		{
			if (mode == EmitMode.ForStorage)
			{
				SaveObjectID();
				SaveSelectorID();
				SaveIsGenerated();
				SaveGeneratorID();
			}
			else
			{
				RemoveObjectID();
				RemoveSelectorID();
				RemoveIsGenerated();
				RemoveGeneratorID();
			}
			
			RepresentationDefinition representation = new RepresentationDefinition(Name);
			foreach (Property property in Properties)
				representation.Properties.Add(property.EmitStatement(mode));
			if (!IsDefaultSelector)
				representation.SelectorAccessorBlock = Selector.Block.EmitAccessorBlock(mode);
			
			representation.MetaData = MetaData == null ? null : MetaData.Copy();
			return representation;
		}
		
		public override Statement EmitStatement(EmitMode mode)
		{
			AlterScalarTypeStatement statement = new AlterScalarTypeStatement();
			statement.ScalarTypeName = ScalarType.Name;
			statement.CreateRepresentations.Add(EmitDefinition(mode));
			return statement;
		}

		public override Statement EmitDropStatement(EmitMode mode)
		{
			AlterScalarTypeStatement statement = new AlterScalarTypeStatement();
			statement.ScalarTypeName = ScalarType.Name;
			statement.DropRepresentations.Add(new DropRepresentationDefinition(Name));
			return statement;
		}

        public override void IncludeDependencies(CatalogDeviceSession session, Catalog sourceCatalog, Catalog targetCatalog, EmitMode mode)
        {
			base.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
			
			Selector.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
				
			foreach (Property property in Properties)
				property.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
        }
        
		public override Object GetObjectFromHeader(ObjectHeader header)
		{
			if (_iD == header.ParentObjectID)
				foreach (Property property in _properties)
					if (header.ID == property.ID)
						return property;

			return base.GetObjectFromHeader(header);
		}

        private PlanNode _readNode;
        public PlanNode ReadNode
        {
			get { return _readNode; }
			set { _readNode = value; }
		}
        
        private PlanNode _writeNode;
        public PlanNode WriteNode
        {
			get { return _writeNode; }
			set { _writeNode = value; }
		}
        
		public bool IsNativeAccessorRepresentation(NativeAccessor nativeAccessor, bool explicitValue)
		{
			return 
				(Name == MetaData.GetTag(MetaData, String.Format("DAE.{0}", nativeAccessor.Name), nativeAccessor.Name)) ||
				(
					!explicitValue &&
					(Properties.Count == 1) && 
					(Properties[0].DataType is Schema.ScalarType) && 
					(((Schema.ScalarType)Properties[0].DataType).NativeType == nativeAccessor.NativeType)
				);
		}
		
        public bool IsNativeAccessorRepresentation(bool explicitValue)
        {
			return 
				IsNativeAccessorRepresentation(NativeAccessors.AsBoolean, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsByte, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsByteArray, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsDateTime, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsDecimal, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsDisplayString, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsException, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsGuid, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsInt16, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsInt32, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsInt64, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsString, explicitValue) ||
				IsNativeAccessorRepresentation(NativeAccessors.AsTimeSpan, explicitValue);
		}
		
		/// <summary>A represntation is persistent if it has external dependencies.</summary>
		public override bool IsPersistent { get { return HasExternalDependencies(); } }

		/// <summary>Returns true is this representation is not the system provided representation and this representation or any of its properties have dependencies on something other than the like type.</summary>
		public bool HasExternalDependencies()
		{
			if ((ScalarType != null) && ScalarType.IsDefaultConveyor && IsDefaultSelector)
				return false;
				
			Schema.ScalarType systemType = (ScalarType != null) && ScalarType.IsDefaultConveyor && IsDefaultSelector && (Properties.Count == 1) ? Properties[0].DataType as Schema.ScalarType : null;
			if (HasDependencies())
			{
				if ((ScalarType != null) && ((ScalarType.LikeType != null) || (systemType != null)))
				{
					for (int index = 0; index < Dependencies.Count; index++)
						if 
						(
							((ScalarType.LikeType != null) && Dependencies.IDs[index] != ScalarType.LikeType.ID) || 
							((systemType != null) && Dependencies.IDs[index] != systemType.ID)
						)
							return true;
				}
				else
					return true;
			}
				
			for (int index = 0; index < _properties.Count; index++)
				if (_properties[index].HasExternalDependencies(systemType))
					return true;
			
			return false;
		}
	}

    /// <remarks> Representations </remarks>
	public class Representations : Objects
    {
		public Representations(ScalarType scalarType) : base()
		{
			_scalarType = scalarType;
		}
		
		[Reference]
		private ScalarType _scalarType;
		public ScalarType ScalarType { get { return _scalarType; } }
	
		#if USEOBJECTVALIDATE
		protected override void Validate(Object item)
		{
			if (!(item is Representation))
				throw new SchemaException(SchemaException.Codes.RepresentationContainer);
			base.Validate(item);
			_scalarType.ValidateChildObjectName(item.Name);
		}
		#endif
		
		protected override void Adding(Object item, int index)
		{
			base.Adding(item, index);
			((Representation)item)._scalarType = _scalarType;
		}
		
		protected override void Removing(Object item, int index)
		{
			((Representation)item)._scalarType = null;
			base.Removing(item, index);
		}

		public new Representation this[int index]
		{
			get { return (Representation)base[index]; }
			set { base[index] = value; }
		}

		public new Representation this[string name]
		{
			get { return (Representation)base[name]; }
			set { base[name] = value; }
		}
    }
    
    public class Sort : CatalogObject
    {
		public Sort(int iD, string name, IDataType dataType) : base(iD, name) 
		{
			_dataType = dataType;
		}

		public Sort(int iD, string name, IDataType dataType, PlanNode compareNode) : base(iD, name) 
		{
			_dataType = dataType;
			_compareNode = compareNode;
		}

		[Reference]
		private IDataType _dataType;
		public IDataType DataType { get { return _dataType; } }
		
		private bool _isUnique;
		public bool IsUnique
		{
			get { return _isUnique; }
			set { _isUnique = value; }
		}
		
		private PlanNode _compareNode;
		public PlanNode CompareNode
		{
			get { return _compareNode; }
			set { _compareNode = value; }
		}
		
		private string GetDataTypeDisplayName()
		{
			if (DataType is Schema.ScalarType)
				return ((Schema.ScalarType)DataType).DisplayName;
			else
				return DataType.Name;
		}
		
		public override string Description { get { return String.Format(Strings.Get("SchemaObjectDescription.Sort"), DisplayName, GetDataTypeDisplayName()); } }
		
		public SortDefinition EmitDefinition(EmitMode mode)
		{
			if (mode == EmitMode.ForStorage)
				SaveObjectID();
			else
				RemoveObjectID();

			SortDefinition sortDefinition = new SortDefinition();
			sortDefinition.Expression = (Expression)_compareNode.EmitStatement(mode);
			sortDefinition.MetaData = MetaData == null ? null : MetaData.Copy();
			return sortDefinition;
		}
		
		public override Statement EmitStatement(EmitMode mode)
		{
			if (mode == EmitMode.ForStorage)
				SaveObjectID();
			else
				RemoveObjectID();

			CreateSortStatement statement = new CreateSortStatement(); 
			statement.ScalarTypeName = Schema.Object.EnsureRooted(DataType.Name);
			statement.Expression = (Expression)_compareNode.EmitStatement(mode);
			statement.MetaData = MetaData == null ? null : MetaData.Copy();
			return statement;
		}
		
		public override Statement EmitDropStatement(EmitMode mode)
		{
			return new DropSortStatement(DataType.Name, _isUnique);
		}

		/// <summary>Returns true if the compare expression for this sort is syntactically equivalent to the compare expression of the given sort.</summary>
		public bool Equivalent(Sort sort)
		{
			if ((_compareNode != null) && (sort.CompareNode != null))
				return Object.ReferenceEquals(_compareNode, sort.CompareNode) || (String.Compare(_compareNode.EmitStatementAsString(), sort.CompareNode.EmitStatementAsString()) == 0);
			else
				return false;
		}

		public override void IncludeDependencies(CatalogDeviceSession session, Catalog sourceCatalog, Catalog targetCatalog, EmitMode mode)
		{
			if (!targetCatalog.Contains(this))
			{
				targetCatalog.Add(this);
				base.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
			}
		}
    }
    
    public class Conversion : CatalogObject
    {
		public Conversion(int iD, string name, ScalarType sourceScalarType, ScalarType targetScalarType, Operator operatorValue, bool isNarrowing) : base(iD, name) 
		{
			_sourceScalarType = sourceScalarType;
			_targetScalarType = targetScalarType;
			_operator = operatorValue;
			_isNarrowing = isNarrowing;
		}

		public override string Description { get { return String.Format(Strings.Get("SchemaObjectDescription.Conversion"), IsNarrowing ? Strings.Get("SchemaObjectDescription.Narrowing") : Strings.Get("SchemaObjectDescription.Widening"), _sourceScalarType.DisplayName, _targetScalarType.DisplayName); } }
		
		[Reference]
		private ScalarType _sourceScalarType;
		public ScalarType SourceScalarType { get { return _sourceScalarType; } }
		
		[Reference]
		private ScalarType _targetScalarType;
		public ScalarType TargetScalarType { get { return _targetScalarType; } }
		
		[Reference]
		private Operator _operator;
		public Operator Operator { get { return _operator; } }
		
		private bool _isNarrowing = true;
		public bool IsNarrowing { get { return _isNarrowing; } }
		
		public override Statement EmitStatement(EmitMode mode)
		{
			if (mode == EmitMode.ForStorage)
			{
				SaveObjectID();
				SaveGeneratorID();
			}
			else
			{
				RemoveObjectID();
				RemoveGeneratorID();
			}

			CreateConversionStatement statement = new CreateConversionStatement();
			statement.SourceScalarTypeName = SourceScalarType.EmitSpecifier(mode);
			statement.TargetScalarTypeName = TargetScalarType.EmitSpecifier(mode);
			statement.OperatorName = new IdentifierExpression(Schema.Object.EnsureRooted(Operator.OperatorName));
			statement.IsNarrowing = IsNarrowing;
			statement.MetaData = MetaData == null ? null : MetaData.Copy();
			if (mode == EmitMode.ForRemote)
			{
				if (statement.MetaData == null)
					statement.MetaData = new MetaData();
				statement.MetaData.Tags.AddOrUpdate("DAE.RootedIdentifier", Schema.Object.EnsureRooted(Name));
			}
			return statement;
		}
		
		public override Statement EmitDropStatement(EmitMode mode)
		{
			DropConversionStatement statement = new DropConversionStatement();
			statement.SourceScalarTypeName = SourceScalarType.EmitSpecifier(mode);
			statement.TargetScalarTypeName = TargetScalarType.EmitSpecifier(mode);
			return statement;
		}
		
		public override void IncludeDependencies(CatalogDeviceSession session, Catalog sourceCatalog, Catalog targetCatalog, EmitMode mode)
		{
			if (!targetCatalog.Contains(Name))
			{
				targetCatalog.Add(this);
				base.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
			}
		}
    }
    
    public class Conversions : Objects
    {
		public new Conversion this[int index]
		{
			get { return (Conversion)base[index]; }
			set { base[index] = value; }
		}
		
		public new Object this[string name]
		{
			get { return (Conversion)base[name]; }
			set { base[name] = value; }
		}
    }
    
	public class ScalarConversionPath : Conversions
	{
		public ScalarConversionPath() : base()
		{
			//FRolloverCount = 4;
		}
		
		public ScalarConversionPath(ScalarConversionPath path) : base()
		{
			//FRolloverCount = 4;
			AddRange(path);
		}

		#if USEOBJECTVALIDATE		
		protected override void Validate(Object objectValue)
		{
			// Don't validate, duplicates will never be added to a path
		}
		#endif
		
		/// <summary>The initial conversion for this conversion path.</summary>
		public Schema.Conversion Conversion { get { return this[0]; } }

		/// <summary>Indicates the degree of narrowing that will occur on this conversion path.  Each narrowing conversion encountered along the path decreases the narrowing score by 1.  If no narrowing conversions occur along this path, then this number is 0. </summary>
		public int NarrowingScore;

		protected override void Adding(Schema.Object objectValue, int index)
		{
			base.Adding(objectValue, index);
			if (((Conversion)objectValue).IsNarrowing)
				NarrowingScore--;
		}

		protected override void Removing(Schema.Object objectValue, int index)
		{
			base.Removing(objectValue, index);
			if (((Conversion)objectValue).IsNarrowing)
				NarrowingScore++;
		}
		
		public override string ToString()
		{
			StringBuilder builder = new StringBuilder();
			foreach (Conversion conversion in this)
			{
				if (builder.Length > 0)
					builder.Append(", ");
				builder.AppendFormat(conversion.Name);
			}
			return builder.ToString();
		}
		
		/// <summary>Returns true if this path goes through the given scalar type.</summary>
		public bool Contains(ScalarType scalarType)
		{
			foreach (Conversion conversion in this)
				if ((scalarType.Equals(conversion.SourceScalarType)) || (scalarType.Equals(conversion.TargetScalarType)))
					return true;
			return false;
		}
	}
	
	#if USETYPEDLIST
	public class ScalarConversionPathList : TypedList
	{
		public ScalarConversionPathList() : base(typeof(ScalarConversionPath)) {}

		public new ScalarConversionPath this[int AIndex]
		{
			get { return (ScalarConversionPath)base[AIndex]; }
			set { base[AIndex] = value; }
		}
	}
	#else
	public class ScalarConversionPathList : ValidatingBaseList<ScalarConversionPath> { }
	#endif
	
	public class ScalarConversionPaths : ScalarConversionPathList
	{
		private int _bestNarrowingScore = Int32.MinValue;
		/// <summary>Indicates the best narrowing score encountered so far.  Conversion paths with lower narrowing scores than this need not be pursued any further.</summary>
		public int BestNarrowingScore { get { return _bestNarrowingScore; } }
		
		private int _shortestLength = Int32.MaxValue;
		/// <summary>Indicates the shortest path length among paths with the current BestNarrowingScore.</summary>
		public int ShortestLength { get { return _shortestLength; } }
		
		private void FindShortestLength()
		{
			_shortestLength = Int32.MaxValue;
			for (int index = 0; index < Count; index++)
				if (this[index].NarrowingScore == BestNarrowingScore)
					if (this[index].Count < _shortestLength)
						_shortestLength = this[index].Count;
		}
		
		private void ComputeBestPaths()
		{
			foreach (ScalarConversionPath path in this)
				if (path.NarrowingScore == _bestNarrowingScore)
					_bestPaths.Add(path);
		}
		
		private void ComputeBestPath()
		{
			_bestPath = null;
			foreach (ScalarConversionPath path in BestPaths)
				if (path.Count == _shortestLength)
					if (_bestPath != null)
					{
						_bestPath = null;
						break;
					}
					else
						_bestPath = path;
		}
		
		#if USETYPEDLIST
		protected override void Adding(object AValue, int AIndex)
		{
			ScalarConversionPath LConversionPath = (ScalarConversionPath)AValue;
		#else
		protected override void Adding(ScalarConversionPath LConversionPath, int index)
		{
		#endif
			if (LConversionPath.NarrowingScore > BestNarrowingScore)
			{
				_bestNarrowingScore = LConversionPath.NarrowingScore;
				FindShortestLength();
			}
			else if (LConversionPath.NarrowingScore == BestNarrowingScore)
				if (LConversionPath.Count < _shortestLength)
					_shortestLength = LConversionPath.Count;
					
			_bestPaths = null;				
			_bestPath = null;
					
			//base.Adding(AValue, AIndex);
		}
		
		private ScalarConversionPathList _bestPaths;
		/// <summary>Contains the set of conversion paths with the current best narrowing score.</summary>
		public ScalarConversionPathList BestPaths 
		{ 
			get 
			{ 
				if (_bestPaths == null)
				{
					_bestPaths = new ScalarConversionPathList();
					_bestPath = null;
					ComputeBestPaths();
					ComputeBestPath();
				}
				return _bestPaths; 
			} 
		}
		
		/// <summary>Returns true if there is only one conversion path with the best narrowing score and shortest length, false otherwise.</summary>
		public bool CanConvert { get { return BestPath != null; } }
		
		private ScalarConversionPath _bestPath;
		/// <summary>Returns the single conversion path with the best narrowing score and shortest path length, null if there are multiple paths with the same narrowing score and path length.</summary>
		public ScalarConversionPath BestPath
		{ 
			get 
			{ 
				if (_bestPaths == null)
				{
					_bestPaths = new ScalarConversionPathList();
					_bestPath = null;
					ComputeBestPaths();
					ComputeBestPath();
				}
				return _bestPath;
			}
		}
	}
	
	public class ScalarConversionPathCache : System.Object
	{
		private Dictionary<EndPoints, ScalarConversionPath> _paths = new Dictionary<EndPoints, ScalarConversionPath>();
		
		private class EndPoints : System.Object
		{
			public EndPoints(Schema.ScalarType sourceType, Schema.ScalarType targetType)
			{
				SourceType = sourceType;
				TargetType = targetType;
			}
			
			[Reference]
			public Schema.ScalarType SourceType;
	
			[Reference]
			public Schema.ScalarType TargetType;
			
			public override bool Equals(object objectValue)
			{
				return (objectValue is EndPoints) && ((EndPoints)objectValue).SourceType.Equals(SourceType) && ((EndPoints)objectValue).TargetType.Equals(TargetType);
			}
			
			public override int GetHashCode()
			{
				return SourceType.GetHashCode() ^ TargetType.GetHashCode();
			}
		}
		
		public ScalarConversionPath this[Schema.ScalarType sourceType, Schema.ScalarType targetType]
		{
			get 
			{
				ScalarConversionPath result;
				if (_paths.TryGetValue(new EndPoints(sourceType, targetType), out result))
					return result;
				else
					return null;
			}
		}
		
		public void Add(Schema.ScalarType sourceType, Schema.ScalarType targetType, Schema.ScalarConversionPath path)
		{
			_paths.Add(new EndPoints(sourceType, targetType), path);
		}
		
		/// <summary>Clears the entire conversion path cache.</summary>
		public void Clear()
		{
			_paths.Clear();
		}
		
		/// <summary>Removes any cache entries for conversion paths which reference the specified scalar type.</summary>
		public void Clear(Schema.ScalarType scalarType)
		{
			List<EndPoints> removeList = new List<EndPoints>();
			foreach (KeyValuePair<EndPoints, ScalarConversionPath> entry in _paths)
				if (entry.Value.Contains(scalarType))
					removeList.Add(entry.Key);
					
			foreach (EndPoints endPoints in removeList)
				_paths.Remove(endPoints);
		}

		/// <summary>Removes any cache entries for conversion paths which reference the specified conversion.</summary>		
		public void Clear(Schema.Conversion conversion)
		{
			List<EndPoints> removeList = new List<EndPoints>();
			foreach (KeyValuePair<EndPoints, ScalarConversionPath> entry in _paths)
				if (entry.Value.Contains(conversion))
					removeList.Add(entry.Key);
					
			foreach (EndPoints endPoints in removeList)
				_paths.Remove(endPoints);
		}
	}
	
	public class Special : Object
    {
		public Special(int iD, string name) : base(iD, name) {}
		public Special(int iD, string name, PlanNode valueNode) : base(iD, name)
		{
			_valueNode = valueNode;
		}
		
		public override string Description { get { return String.Format(Strings.Get("SchemaObjectDescription.Special"), DisplayName, _scalarType.DisplayName); } }
		
		/// <summary>Specials are always persistent.</summary>
		public override bool IsPersistent { get { return true; } }

		public override int CatalogObjectID { get { return _scalarType == null ? -1 : _scalarType.ID; } }
		
		public override int ParentObjectID { get { return _scalarType == null ? -1 : _scalarType.ID; } }

		[Reference]
		internal ScalarType _scalarType;
		public ScalarType ScalarType
		{
			get { return _scalarType; }
			set
			{
				if (_scalarType != null)
					_scalarType.Specials.Remove(this);
				if (value != null)
					value.Specials.Add(this);
			}
		}

		private PlanNode _valueNode;		
		public PlanNode ValueNode
		{
			get { return _valueNode; }
			set { _valueNode = value; }
		}
		
		private int _selectorID = -1;
		public int SelectorID
		{
			get { return _selectorID; }
			set { _selectorID = value; }
		}
		
		public void LoadSelectorID()
		{
			Tag tag = MetaData.RemoveTag(MetaData, "DAE.SelectorID");
			if (tag != Tag.None)
				_selectorID = Int32.Parse(tag.Value);
		}

		public void SaveSelectorID()
		{
			MetaData.Tags.AddOrUpdate("DAE.SelectorID", _selectorID.ToString(), true);
		}
		
		public void RemoveSelectorID()
		{
			if (MetaData != null)
				MetaData.Tags.RemoveTag("DAE.SelectorID");
		}

		public void ResolveSelector(CatalogDeviceSession session)
		{
			if ((_selector == null) && (_selectorID > -1))
				_selector = session.ResolveCatalogObject(_selectorID) as Schema.Operator;
		}

		[Reference]
		private Operator _selector;
		public Operator Selector
		{
			get { return _selector; }
			set 
			{ 
				_selector = value; 
				_selectorID = value == null ? -1 : value.ID;
			}
		}
		
		private int _comparerID = -1;
		public int ComparerID
		{
			get { return _comparerID; }
			set { _comparerID = value; }
		}
		
		public void LoadComparerID()
		{
			Tag tag = MetaData.RemoveTag(MetaData, "DAE.ComparerID");
			if (tag != Tag.None)
				_comparerID = Int32.Parse(tag.Value);
		}

		public void SaveComparerID()
		{
			MetaData.Tags.AddOrUpdate("DAE.ComparerID", _comparerID.ToString(), true);
		}
		
		public void RemoveComparerID()
		{
			if (MetaData != null)
				MetaData.Tags.RemoveTag("DAE.ComparerID");
		}

		public void ResolveComparer(CatalogDeviceSession session)
		{
			if ((_comparer == null) && (_comparerID > -1))
				_comparer = session.ResolveCatalogObject(_comparerID) as Schema.Operator;
		}

		[Reference]
		private Operator _comparer;
		public Operator Comparer
		{
			get { return _comparer; }
			set 
			{ 
				_comparer = value; 
				_comparerID = value == null ? -1 : value.ID;
			}
		}
		
		public SpecialDefinition EmitDefinition(EmitMode mode)
		{
			if (mode == EmitMode.ForStorage)
			{
				SaveObjectID();
				SaveIsGenerated();
				SaveGeneratorID();
				SaveSelectorID();
				SaveComparerID();
			}
			else
			{
				RemoveObjectID();
				RemoveIsGenerated();
				RemoveGeneratorID();
				RemoveSelectorID();
				RemoveComparerID();
			}
			
			SpecialDefinition special = new SpecialDefinition();
			special.Name = Name;
			special.Value = (Expression)ValueNode.EmitStatement(mode);
			special.MetaData = MetaData == null ? null : MetaData.Copy();
			return special;
		}
		
		public override Statement EmitStatement(EmitMode mode)
		{
			AlterScalarTypeStatement statement = new AlterScalarTypeStatement();
			statement.ScalarTypeName = ScalarType.Name;
			statement.CreateSpecials.Add(EmitDefinition(mode));
			return statement;
		}

		public override Statement EmitDropStatement(EmitMode mode)
		{
			AlterScalarTypeStatement alterStatement = new AlterScalarTypeStatement();
			alterStatement.ScalarTypeName = ScalarType.Name;
			alterStatement.DropSpecials.Add(new DropSpecialDefinition(Name));
			return alterStatement;
		}

		public override void IncludeDependencies(CatalogDeviceSession session, Catalog sourceCatalog, Catalog targetCatalog, EmitMode mode)
		{
			base.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
			
			Selector.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
			Comparer.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
		}
    }

    /// <remarks> Specials </remarks>
	public class Specials : Objects
    {
		public Specials(ScalarType scalarType) : base()
		{
			_scalarType = scalarType;
		}
		
		[Reference]
		private ScalarType _scalarType;
		public ScalarType ScalarType { get { return _scalarType; } }
		
		#if USEOBJECTVALIDATE
		protected override void Validate(Object item)
		{
			if (!(item is Special))
				throw new SchemaException(SchemaException.Codes.SpecialContainer);
			base.Validate(item);
		}
		#endif
		
		protected override void Adding(Object item, int index)
		{
			base.Adding(item, index);
			((Special)item)._scalarType = _scalarType;
		}
		
		protected override void Removing(Object item, int index)
		{
			((Special)item)._scalarType = null;
			base.Removing(item, index);
		}

		public new Special this[int index]
		{
			get { return (Special)base[index]; }
			set { base[index] = value; }
		}

		public new Special this[string name]
		{
			get { return (Special)base[name]; }
			set { base[name] = value; }
		}
    }
    
    public interface IScalarType : IDataType
    {
		ScalarTypeConstraints Constraints { get; }
		#if USETYPEINHERITANCE
		ScalarTypes ParentTypes { get; }
		#endif
    }
    
	/// <remarks> Implements the representation of scalar data types. </remarks>
	public class ScalarType : CatalogObject, IScalarType
    {
		// constructor
		public ScalarType(int iD, string name) : base(iD, name)
		{
			InternalInitialize();
		}

		private void InternalInitialize()
		{
			_isDisposable = true;
			#if USETYPEINHERITANCE
			FParentTypes = new ScalarTypes();
			//FParentTypes.OnValidate += new SchemaObjectListEventHandler(ChildObjectValidate);
			#endif
			_representations = new Representations(this);
			//FRepresentations.OnValidate += new SchemaObjectListEventHandler(ChildObjectValidate);
			_specials = new Specials(this);
			//FSpecials.OnValidate += new SchemaObjectListEventHandler(ChildObjectValidate);
			_constraints = new ScalarTypeConstraints(this);
			//FConstraints.OnValidate += new SchemaObjectListEventHandler(ChildObjectValidate);
		}

		public override string[] GetRights()
		{
			return new string[]
			{
				Name + Schema.RightNames.Alter,
				Name + Schema.RightNames.Drop
			};
		}

		public override string Description { get { return String.Format(Strings.Get("SchemaObjectDescription.ScalarType"), DisplayName); } }

		// IsGeneric
		// Indicates whether this data type is a generic data type (i.e. table, not table{})
		private bool _isGeneric;
		public bool IsGeneric
		{
			get { return _isGeneric; }
			set { _isGeneric = value; }
		}
		
		public bool IsNil { get { return false; } }
		
		// IsDisposable
		// Indicates whether the host representation for this data type must be disposed
		private bool _isDisposable = false;
		public bool IsDisposable
		{
			get { return _isDisposable; }
			set { _isDisposable = value; }
		}
		
		// IsCompound
		// Indicates whether the native representation for this type is multi-property and system-provided
		public bool IsCompound;
		
		public Schema.IRowType CompoundRowType;
		
		public void ValidateChildObjectName(string name)
		{
			if 
			(
				(_constraints.IndexOfName(name) >= 0) 
					|| ((_default != null) && (String.Compare(_default.Name, name) == 0)) 
					|| (_representations.IndexOfName(name) >= 0)
			)
				throw new SchemaException(SchemaException.Codes.DuplicateChildObjectName, name);
		}
		
		public bool Equivalent(IDataType dataType)
		{
			return Equals(dataType);
		}
		
		public bool Equals(IDataType dataType)
		{
			return (dataType is IScalarType) && Schema.Object.NamesEqual(Name, dataType.Name);
		}

		// Is
		public bool Is(IDataType dataType)
		{
			if (dataType is IGenericType)
				return true;
			else if (dataType is IScalarType)
			{
				if (!this.Equals(dataType))
				{
					if (dataType.IsGeneric)
						return true;
					
					#if USETYPEINHERITANCE	
					foreach (IScalarType parentType in FParentTypes)
						if (parentType.Is(ADataType))
							return true;
					#endif

					return false;
				}
				return true;
			}
			return false;
		}

        // IsLike
        public bool IsLike(IDataType dataType)
        {
            if (!this.Equals(dataType))
            {
                if (_likeType != null)
                {
                    return _likeType.IsLike(dataType);
                }
                return false;
            }
            return true;
        }

		// Compatible
		// Compatible is A is B or B is A
		public bool Compatible(IDataType dataType)
		{
			return Is(dataType) || dataType.Is(this);
		}

		#if NATIVEROW
/*
		public int GetByteSize(object AValue)
		{
		}
*/
		#else
		// Indicates the physical size of data to be stored in the index nodes before using overflow streams
		// Must be at least Index.MinimumStaticByteSize to support overflow streams
		private int FStaticByteSize;
		public int StaticByteSize
		{
			get { return FStaticByteSize; }
			set { FStaticByteSize = value; }
		}
		#endif

        // Class Definition
        private ClassDefinition _classDefinition;
		public ClassDefinition ClassDefinition
        {
			get { return _classDefinition; }
			set { _classDefinition = value; }
        }
        
		private bool _isDefaultConveyor;
		public bool IsDefaultConveyor
		{
			get { return _isDefaultConveyor; }
			set { _isDefaultConveyor = value; }
		}
		
		public void LoadIsDefaultConveyor()
		{
			Tag tag = MetaData.RemoveTag(MetaData, "DAE.IsDefaultConveyor");
			if (tag != Tag.None)
				_isDefaultConveyor = Boolean.Parse(tag.Value);
		}

		public void SaveIsDefaultConveyor()
		{
			MetaData.Tags.AddOrUpdate("DAE.IsDefaultConveyor", _isDefaultConveyor.ToString(), true);
		}

		public void RemoveIsDefaultConveyor()
		{
			if (MetaData != null)
				MetaData.Tags.RemoveTag("DAE.IsDefaultConveyor");
		}
		
		[Reference]
		private Type _nativeType;
		public Type NativeType
		{
			get { return _nativeType; }
			set { _nativeType = value; }
		}
		
		public bool HasRepresentation(NativeAccessor nativeAccessor)
		{
			return HasRepresentation(nativeAccessor, false);
		}
		
		public bool HasRepresentation(NativeAccessor nativeAccessor, bool explicitValue)
		{
			return FindRepresentation(nativeAccessor, explicitValue) != null;
		}
		
		public Schema.Representation FindRepresentation(NativeAccessor nativeAccessor)
		{
			return FindRepresentation(nativeAccessor, false);
		}
		
		private object _nativeRepresentationsHandle = new object(); // sync handle for the native representation cache
		private Dictionary<string, NativeRepresentation> _nativeRepresentations;
		protected Dictionary<string, NativeRepresentation> NativeRepresentations 
		{ 
			get 
			{ 
				if (_nativeRepresentations == null)
					_nativeRepresentations = new Dictionary<string, NativeRepresentation>(); 
				return _nativeRepresentations; 
			} 
		}

		protected class NativeRepresentation
		{
			public NativeRepresentation(Representation representation, bool explicitValue)
			{
				Representation = representation;
				Explicit = explicitValue;
			}
			
			[Reference]
			public Representation Representation;
			public bool Explicit;
		}
		
		public void ResetNativeRepresentationCache()
		{
			lock (_nativeRepresentationsHandle)
			{
				_nativeRepresentations = null;
			}
		}
		
		private NativeRepresentation FindNativeRepresentation(NativeAccessor nativeAccessor)
		{
			string representationName = MetaData.GetTag(MetaData, String.Format("DAE.{0}", nativeAccessor.Name), String.Empty);
			int representationIndex = _representations.IndexOf(representationName);
			if (representationIndex >= 0)
				return new NativeRepresentation(_representations[representationIndex], true);

			foreach (Schema.Representation representation in _representations)
				if ((representation.Properties.Count == 1) && (representation.Properties[0].DataType is Schema.ScalarType) && (((Schema.ScalarType)representation.Properties[0].DataType).NativeType == nativeAccessor.NativeType))
					return new NativeRepresentation(representation, false);
			
			return new NativeRepresentation(null, false);
		}
		
		public Schema.Representation FindRepresentation(NativeAccessor nativeAccessor, bool explicitValue)
		{
			NativeRepresentation nativeRepresentation;

			lock (_nativeRepresentationsHandle)
			{
				if (!NativeRepresentations.TryGetValue(nativeAccessor.Name, out nativeRepresentation))
				{
					nativeRepresentation = FindNativeRepresentation(nativeAccessor);
					NativeRepresentations.Add(nativeAccessor.Name, nativeRepresentation);
				}
			}

			if (explicitValue && !nativeRepresentation.Explicit)
				return null;
				
			return nativeRepresentation.Representation;			
		}
		
		public Schema.Representation GetRepresentation(NativeAccessor nativeAccessor)
		{
			Schema.Representation representation = FindRepresentation(nativeAccessor);
			if (representation == null)
				throw new SchemaException(SchemaException.Codes.UnableToLocateConversionRepresentation, Name, nativeAccessor.Name);
			return representation;
		}
		
		#if USETYPEINHERITANCE
        // ParentTypes
        private ScalarTypes FParentTypes;
		public ScalarTypes ParentTypes { get { return FParentTypes; } }
		#endif
		
		// LikeType
		[Reference]
		private ScalarType _likeType;
		public ScalarType LikeType 
		{ 
			get { return _likeType; } 
			set { _likeType = value; }
		}
		
        // Representations
        private Representations _representations;
        public Representations Representations { get { return _representations; } }

		// Constraints
		private ScalarTypeConstraints _constraints;
		public ScalarTypeConstraints Constraints { get { return _constraints; } }

        // Specials
        private Specials _specials;
        public Specials Specials { get { return _specials; } }
        
		private int _isSpecialOperatorID = -1;
		public int IsSpecialOperatorID
		{
			get { return _isSpecialOperatorID; }
			set { _isSpecialOperatorID = value; }
		}
		
		public void LoadIsSpecialOperatorID()
		{
			Tag tag = MetaData.RemoveTag(MetaData, "DAE.IsSpecialOperatorID");
			if (tag != Tag.None)
				_isSpecialOperatorID = Int32.Parse(tag.Value);
		}

		public void SaveIsSpecialOperatorID()
		{
			MetaData.Tags.AddOrUpdate("DAE.IsSpecialOperatorID", _isSpecialOperatorID.ToString(), true);
		}
		
		public void RemoveIsSpecialOperatorID()
		{
			if (MetaData != null)
				MetaData.Tags.RemoveTag("DAE.IsSpecialOperatorID");
		}
		
		public void ResolveIsSpecialOperator(CatalogDeviceSession session)
		{
			if ((_isSpecialOperator == null) && (_isSpecialOperatorID > -1))
				_isSpecialOperator = session.ResolveCatalogObject(_isSpecialOperatorID) as Schema.Operator;
		}

        // IsSpecialOperator
		[Reference]
        private Operator _isSpecialOperator;
        public Operator IsSpecialOperator
        {
			get { return _isSpecialOperator; }
			set 
			{ 
				_isSpecialOperator = value; 
				_isSpecialOperatorID = value == null ? -1 : value.ID;
			}
        }
        
		private int _equalityOperatorID = -1;
		public int EqualityOperatorID
		{
			get { return _equalityOperatorID; }
			set { _equalityOperatorID = value; }
		}
		
		public void LoadEqualityOperatorID()
		{
			Tag tag = MetaData.RemoveTag(MetaData, "DAE.EqualityOperatorID");
			if (tag != Tag.None)
				_equalityOperatorID = Int32.Parse(tag.Value);
		}

		public void SaveEqualityOperatorID()
		{
			MetaData.Tags.AddOrUpdate("DAE.EqualityOperatorID", _equalityOperatorID.ToString(), true);
		}

		public void RemoveEqualityOperatorID()
		{
			if (MetaData != null)
				MetaData.Tags.RemoveTag("DAE.EqualityOperatorID");
		}
		
		public void ResolveEqualityOperator(CatalogDeviceSession session)
		{
			if ((_equalityOperator == null) && (_equalityOperatorID > -1))
				_equalityOperator = session.ResolveCatalogObject(_equalityOperatorID) as Schema.Operator;
		}

        // EqualityOperator
		[Reference]
        private Operator _equalityOperator;
        public Operator EqualityOperator
        {
			get { return _equalityOperator; }
			set 
			{ 
				_equalityOperator = value; 
				_equalityOperatorID = value == null ? -1 : value.ID;
			}
		}

		private int _comparisonOperatorID = -1;
		public int ComparisonOperatorID
		{
			get { return _comparisonOperatorID; }
			set { _comparisonOperatorID = value; }
		}
		
		public void LoadComparisonOperatorID()
		{
			Tag tag = MetaData.RemoveTag(MetaData, "DAE.ComparisonOperatorID");
			if (tag != Tag.None)
				_comparisonOperatorID = Int32.Parse(tag.Value);
		}

		public void SaveComparisonOperatorID()
		{
			MetaData.Tags.AddOrUpdate("DAE.ComparisonOperatorID", _comparisonOperatorID.ToString(), true);
		}

		public void RemoveComparisonOperatorID()
		{
			if (MetaData != null)
				MetaData.Tags.RemoveTag("DAE.ComparisonOperatorID");
		}
		
		public void ResolveComparisonOperator(CatalogDeviceSession session)
		{
			if ((_comparisonOperator == null) && (_comparisonOperatorID > -1))
				_comparisonOperator = session.ResolveCatalogObject(_comparisonOperatorID) as Schema.Operator;
		}

        // ComparisonOperator
		[Reference]
        private Operator _comparisonOperator;
        public Operator ComparisonOperator
        {
			get { return _comparisonOperator; }
			set 
			{ 
				_comparisonOperator = value; 
				_comparisonOperatorID = value == null ? -1 : value.ID;
			}
        }

		// Default
		private ScalarTypeDefault _default;
		public ScalarTypeDefault Default
		{
			get { return _default; }
			set
			{
				if (_default != value)
				{
					ScalarTypeDefault FOldDefault = _default;
					_default = null;
					try
					{
						if (value != null)
							ValidateChildObjectName(value.Name);
						if (FOldDefault != null)
							FOldDefault._scalarType = null;
						_default = value;
						if (_default != null)
							_default._scalarType = this;
					}
					catch
					{
						_default = FOldDefault;
						throw;
					}
				}
			}
		}
		
		private int _sortID = -1;
		public int SortID
		{
			get { return _sortID; }
			set { _sortID = value; }
		}
		
		public void LoadSortID()
		{
			Tag tag = MetaData.RemoveTag(MetaData, "DAE.SortID");
			if (tag != Tag.None)
			_sortID = Int32.Parse(tag.Value);
		}

		public void SaveSortID()
		{
			MetaData.Tags.AddOrUpdate("DAE.SortID", _sortID.ToString(), true);
		}
		
		public void RemoveSortID()
		{
			if (MetaData != null)
				MetaData.Tags.RemoveTag("DAE.SortID");
		}
		
		[Reference]
		private Sort _sort;
		public Sort Sort
		{
			get { return _sort; }
			set 
			{ 
				_sort = value; 
				_sortID = value == null ? -1 : value.ID;
			}
		}
		
		private int _uniqueSortID = -1;
		public int UniqueSortID
		{
			get { return _uniqueSortID; }
			set { _uniqueSortID = value; }
		}
		
		public void LoadUniqueSortID()
		{
			Tag tag = MetaData.RemoveTag(MetaData, "DAE.UniqueSortID");
			if (tag != Tag.None)
				_uniqueSortID = Int32.Parse(tag.Value);
		}

		public void SaveUniqueSortID()
		{
			MetaData.Tags.AddOrUpdate("DAE.UniqueSortID", _uniqueSortID.ToString(), true);
		}

		public void RemoveUniqueSortID()
		{
			if (MetaData != null)
				MetaData.Tags.RemoveTag("DAE.UniqueSortID");
		}
		
		[Reference]
		private Sort _uniqueSort;
		public Sort UniqueSort
		{
			get { return _uniqueSort; }
			set 
			{ 
				_uniqueSort = value; 
				_uniqueSortID = value == null ? -1 : value.ID;
			}
		}

		// HasHandlers
		public bool HasHandlers()
		{
			return (_eventHandlers != null) && (_eventHandlers.Count > 0);
		}
		
		public bool HasHandlers(EventType eventType)
		{
			return (_eventHandlers != null) && _eventHandlers.HasHandlers(eventType);
		}
		
		// EventHandlers
		private ScalarTypeEventHandlers _eventHandlers;
		public ScalarTypeEventHandlers EventHandlers 
		{ 
			get 
			{ 
				if (_eventHandlers == null)
					_eventHandlers = new ScalarTypeEventHandlers(this);
				return _eventHandlers; 
			} 
		}

		#if USETYPEINHERITANCE	
		// ExplicitCastOperators
		private Objects FExplicitCastOperators = new Objects();
		public Objects ExplicitCastOperators { get { return FExplicitCastOperators; } }
		#endif

		// ImplicitConversions
		private Conversions _implicitConversions = new Conversions();
		public Conversions ImplicitConversions { get { return _implicitConversions; } }
		
		#if USEPROPOSABLEEVENTS
        // OnValidateValue
        public event ColumnValidateHandler OnValidateValue;
        public virtual void DoValidateValue(IServerSession ASession, object AValue)
        {
            if (OnValidateValue != null)
                OnValidateValue(this, ASession, AValue);
        }

        // OnChangeValue
        public event ColumnChangeHandler OnChangeValue;
        public virtual bool DoChangeValue(IServerSession ASession, ref object AValue)
        {
            bool LChanged = false;
            if (OnChangeValue != null)
                OnChangeValue(this, ASession, ref AValue, out LChanged);
            return LChanged;
        }

        // OnDefaultValue
        public event ColumnChangeHandler OnDefaultValue;
        public virtual bool DoDefaultValue(IServerSession ASession, ref object AValue)
        {
            bool LChanged = false;
            if (OnDefaultValue != null)
                OnDefaultValue(this, ASession, ref AValue, out LChanged);
            return LChanged;
        }
        #endif

        public override void IncludeDependencies(CatalogDeviceSession session, Catalog sourceCatalog, Catalog targetCatalog, EmitMode mode)
        {
			if (!targetCatalog.Contains(Name))
			{
				base.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
				
				targetCatalog.Add(this);
			
				if ((Default != null) && ((mode != EmitMode.ForRemote) || Default.IsRemotable))
					Default.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
					
				foreach (Constraint constraint in Constraints)
					if ((mode != EmitMode.ForRemote) || constraint.IsRemotable)
						constraint.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
						
				foreach (Representation representation in Representations)
					representation.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
					
				if (_isSpecialOperator != null)
					_isSpecialOperator.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);

				foreach (Special special in Specials)
					special.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
					
			}
        }

        public override void IncludeHandlers(CatalogDeviceSession session, Catalog sourceCatalog, Catalog targetCatalog, EmitMode mode)
        {
			if (_eventHandlers != null)
				foreach (EventHandler handler in _eventHandlers)
					if ((mode != EmitMode.ForRemote) || handler.IsRemotable)
						handler.IncludeDependencies(session, sourceCatalog, targetCatalog, mode);
        }
        
        public override Statement EmitStatement(EmitMode mode)
        {
			if (mode == EmitMode.ForStorage)
			{
				SaveObjectID();
				SaveIsSpecialOperatorID();
				SaveEqualityOperatorID();
				SaveComparisonOperatorID();
				SaveSortID();
				SaveUniqueSortID();
			}
			else
			{
				RemoveObjectID();
				RemoveIsSpecialOperatorID();
				RemoveEqualityOperatorID();
				RemoveComparisonOperatorID();
				RemoveSortID();
				RemoveUniqueSortID();
			}

			CreateScalarTypeStatement statement = new CreateScalarTypeStatement();
			statement.ScalarTypeName = Schema.Object.EnsureRooted(Name);
			#if USETYPEINHERITANCE
			foreach (ScalarType parentType in ParentTypes)
				statement.ParentScalarTypes.Add(new ScalarTypeNameDefinition(parentType.Name));
			#endif
			
			if (LikeType != null)
				statement.LikeScalarTypeName = LikeType.Name;

			foreach (Representation representation in Representations)
				if ((!representation.IsGenerated || (mode == EmitMode.ForStorage)) && !representation.HasExternalDependencies())
					statement.Representations.Add(representation.EmitDefinition(mode));
					
			// specials, representations w/dependencies, constraints and defaults are emitted by the catalog as alter statements because they may have dependencies on operators that are undefined when the create scalar type statement is executed.

			statement.ClassDefinition = IsDefaultConveyor || (_classDefinition == null) ? null : (ClassDefinition)_classDefinition.Clone();
			statement.MetaData = MetaData == null ? null : MetaData.Copy();
			return statement;
        }
        
		public override Statement EmitDropStatement(EmitMode mode)
		{
			DropScalarTypeStatement statement = new DropScalarTypeStatement();
			statement.ObjectName = Name;
			return statement;
		}

        public TypeSpecifier EmitSpecifier(EmitMode mode)
        {
			return new ScalarTypeSpecifier(Name);
        }
        
		public override Object GetObjectFromHeader(ObjectHeader header)
		{
			switch (header.ObjectType)
			{
				case "ScalarTypeConstraint" :
					foreach (Constraint constraint in Constraints)
						if (header.ID == constraint.ID)
							return constraint;
				break;
						
				case "Representation" :
					foreach (Representation representation in Representations)
						if (header.ID == representation.ID)
							return representation;
				break;
				
				case "Property" :
					foreach (Representation representation in Representations)
						if (header.ParentObjectID == representation.ID)
							return representation.GetObjectFromHeader(header);
				break;
				
				case "Special" :
					foreach (Special special in _specials)
						if (header.ID == special.ID)
							return special;
				break;
						
				case "ScalarTypeDefault" :
					if ((_default != null) && (header.ID == _default.ID))
						return _default;
				break;
			}
			
			return base.GetObjectFromHeader(header);
		}

		public void ResolveGeneratedDependents(CatalogDeviceSession session)
		{
			if (_representations.Count > 0)
			{
				ResolveEqualityOperator(session);
				ResolveComparisonOperator(session);
				ResolveIsSpecialOperator(session);
				
				foreach (Schema.Representation representation in _representations)
				{
					representation.ResolveSelector(session);
					foreach (Schema.Property property in representation.Properties)
					{
						property.ResolveReadAccessor(session);
						property.ResolveWriteAccessor(session);
					}
				}
				
				foreach (Schema.Special special in _specials)
				{
					special.ResolveSelector(session);
					special.ResolveComparer(session);
				}
			}
		}
	}

    /// <remarks> ScalarTypes </remarks>
	public class ScalarTypes : Objects
    {
		#if USEOBJECTVALIDATE
		protected override void Validate(Object item)
		{
			if (!(item is ScalarType))
				throw new SchemaException(SchemaException.Codes.ScalarTypeContainer);
			base.Validate(item);
		}
		#endif

		public new ScalarType this[int index]
		{
			get { return (ScalarType)base[index]; }
			set { base[index] = value; }
		}

		public new ScalarType this[string name]
		{
			get { return (ScalarType)base[name]; }
			set { base[name] = value; }
		}
    }
} 