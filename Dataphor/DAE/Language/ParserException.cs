/*
	Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/
using System;
using System.Text;
using System.Resources;

using Alphora.Dataphor.DAE;

namespace Alphora.Dataphor.DAE.Language
{
	/// <summary>Indicates an exception encountered while attempt to construct an abstract syntax tree from a lexical stream.</summary>
	/// <remarks>
	/// The ParserException is thrown when the parser encounters an invalid token in the input stream.
	/// These exceptions indicate an invalid input string is being parsed.
	/// Only the parser should throw exceptions of this type.
	/// </remarks>
	[Serializable]
	public class ParserException : DAEException
	{
		public enum Codes : int
		{
			/// <summary>Error code 109100: "Statement expected."</summary>
			StatementExpected = 109100,

			/// <summary>Error code 109101: "Unknown token type "{0}"."</summary>
			UnknownTokenType = 109101,

			/// <summary>Error code 109102: ""," or "}" expected."</summary>
			ListTerminatorExpected = 109102,

			/// <summary>Error code 109103: ""," or ")" expected."</summary>
			GroupTerminatorExpected = 109103,

			/// <summary>Error code 109104: "";" or EOF expected."</summary>
			StatementTerminatorExpected = 109104,

			/// <summary>Error code 109105: "Case item expression expected."</summary>
			CaseItemExpressionExpected = 109105,

			/// <summary>Error code 109106: "Unknown create directive "{0}"."</summary>
			UnknownCreateDirective = 109106,

			/// <summary>Error code 109107: "Unknown create type directive "{0}"."</summary>
			UnknownCreateScalarTypeDirective = 109107,

			/// <summary>Error code 109108: "Unknown alter directive "{0}"."</summary>
			UnknownAlterDirective = 109108,

			/// <summary>Error code 109109: "Unknown alter type directive "{0}"."</summary>
			UnknownAlterScalarTypeDirective = 109109,

			/// <summary>Error code 109110: "Unknown drop directive "{0}"."</summary>
			UnknownDropDirective = 109110,

			/// <summary>Error code 109111: "Unknown reference action "{0}"."</summary>
			UnknownReferenceAction = 109111,

			/// <summary>Error code 109112: "Default definition already specified."</summary>
			DefaultDefinitionExists = 109112,

			/// <summary>Error code 109113: "Override directive not allowed after reintroduce."</summary>
			InvalidOverrideDirective = 109113,

			/// <summary>Error code 109114: "Operator marked as abstract cannot have a method body."</summary>
			InvalidAbstractDirective = 109114,

			/// <summary>Error code 109115: "Operator must have either a class definition or a body defined."</summary>
			InvalidOperatorDefinition = 109115,

			/// <summary>Error code 109116: "Invalid parameter modifier."</summary>
			InvalidParameterModifier = 109116,

			/// <summary>Error code 109117: ""{0}" is not a valid identifier."</summary>
			InvalidIdentifier = 109117,

			/// <summary>Error code 109118: ""{0}" is a reserved word and may not be used as an identifier."</summary>
			ReservedWordIdentifier = 109118,

			/// <summary>Error code 109119: "finally or except expected."</summary>
			TryStatementExpected = 109119,

			/// <summary>Error code 109120: "Schema definition directive expected."</summary>
			DDLDirectiveExpected = 109120,
			
			/// <summary>Error code 109121: "Type specifier already set for selector expression."</summary>
			TypeSpecifierSet = 109121,
			
			/// <summary>Error code 109122: "Unknown event specifier "{0}"."</summary>
			UnknownEventSpecifier = 109122,
			
			/// <summary>Error code 109123: "Event specifier list contains incompatible event specifiers."</summary>
			InvalidEventSpecifierList = 109123,
			
			/// <summary>Error code 109124: "Sort definition already specified."</summary>
			SortDefinitionExists = 109124,
			
			/// <summary>Error code 109125: "Invalid right specifier ('all', 'usage', or '{&lt;list of rights&gt;}' expected)."</summary>
			InvalidRightSpecifier = 109125,

			/// <summary>Error code 109126: "Invalid security specifier."</summary>
			InvalidSecuritySpecifier = 109126,
			
			/// <summary>Error code 109127: "Unknown constraint target."</summary>
			UnknownConstraintTarget = 109127,

			/// <summary>Error code 109128: "Column extractor expression must reference a single column unless invoking an aggregate operator."</summary>
			InvalidColumnExtractorExpression = 109128
		}

		// Resource manager for this exception class
		private static ResourceManager FResourceManager = new ResourceManager("Alphora.Dataphor.DAE.Language.ParserException", typeof(ParserException).Assembly);

		// Constructors
		public ParserException(Codes AErrorCode) : base(FResourceManager, (int)AErrorCode, ErrorSeverity.Application, null, null) {}
		public ParserException(Codes AErrorCode, params object[] AParams) : base(FResourceManager, (int)AErrorCode, ErrorSeverity.Application, null, AParams) {}
		public ParserException(Codes AErrorCode, Exception AInnerException) : base(FResourceManager, (int)AErrorCode, ErrorSeverity.Application, AInnerException, null) {}
		public ParserException(Codes AErrorCode, Exception AInnerException, params object[] AParams) : base(FResourceManager, (int)AErrorCode, ErrorSeverity.Application, AInnerException, AParams) {}
		public ParserException(Codes AErrorCode, ErrorSeverity ASeverity) : base(FResourceManager, (int)AErrorCode, ASeverity, null, null) {}
		public ParserException(Codes AErrorCode, ErrorSeverity ASeverity, params object[] AParams) : base(FResourceManager, (int)AErrorCode, ASeverity, null, AParams) {}
		public ParserException(Codes AErrorCode, ErrorSeverity ASeverity, Exception AInnerException) : base(FResourceManager, (int)AErrorCode, ASeverity, AInnerException, null) {}
		public ParserException(Codes AErrorCode, ErrorSeverity ASeverity, Exception AInnerException, params object[] AParams) : base(FResourceManager, (int)AErrorCode, ASeverity, AInnerException, AParams) {}
		public ParserException(System.Runtime.Serialization.SerializationInfo AInfo, System.Runtime.Serialization.StreamingContext AContext) : base(AInfo, AContext) {}
	}

	public class ParserMessages : TypedList
	{
		public ParserMessages() : base(typeof(Exception)) {}
		
		public new Exception this[int AIndex]
		{
			get { return (Exception)base[AIndex]; }
			set { base[AIndex] = value; }
		}

		/// <summary>Returns true if there are any errors.</summary>		
		public bool HasErrors()
		{
			return Count > 0;
		}
		
		public override string ToString()
		{
			StringBuilder LBuilder = new StringBuilder();
			foreach (Exception LException in this)
				ExceptionUtility.AppendMessage(LBuilder, 0, LException);
			return LBuilder.ToString();
		}
	}
}