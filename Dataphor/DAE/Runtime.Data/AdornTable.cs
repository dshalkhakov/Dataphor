/*
	Dataphor
	© Copyright 2000-2008 Alphora
	This file is licensed under a modified BSD-license which can be found here: http://dataphor.org/dataphor_license.txt
*/
namespace Alphora.Dataphor.DAE.Runtime.Data
{
	using System;
	using System.Collections;
	using System.Diagnostics;

	using Alphora.Dataphor;
	using Alphora.Dataphor.DAE;
	using Alphora.Dataphor.DAE.Server;
	using Alphora.Dataphor.DAE.Streams;
	using Alphora.Dataphor.DAE.Runtime;
	using Alphora.Dataphor.DAE.Runtime.Data;
	using Alphora.Dataphor.DAE.Runtime.Instructions;
	using Alphora.Dataphor.DAE.Language.D4;
	using Alphora.Dataphor.DAE.Device.Memory;
	using Schema = Alphora.Dataphor.DAE.Schema;

    public class AdornTable : Table
    {
        public AdornTable(AdornNode ANode, ServerProcess AProcess) : base(ANode, AProcess){}
        
        public new AdornNode Node { get { return (AdornNode)FNode; } }
        
		protected DataVar FSourceObject;        
		protected Table FSourceTable;
		protected Row FSourceRow;
		protected DataVar FSourceRowObject;
		protected bool FBOF;
        
        protected override void InternalOpen()
        {
			FSourceObject = Node.Nodes[0].Execute(Process);
			try
			{
				FSourceTable = (Table)FSourceObject.Value;
				FSourceRow = new Row(Process, FSourceTable.DataType.RowType);
				FSourceRowObject = new DataVar(String.Empty, FSourceRow.DataType, FSourceRow);
			}
			catch
			{
				((Table)FSourceObject.Value).Dispose();
				throw;
			}
			FBOF = true;
        }
        
        protected override void InternalClose()
        {
			if (FSourceTable != null)
			{
				FSourceTable.Dispose();
				FSourceTable = null;
			}

            if (FSourceRow != null)
            {
				FSourceRow.Dispose();
				FSourceRow = null;
            }
        }
        
        protected override void InternalReset()
        {
            FSourceTable.Reset();
            FBOF = true;
        }
        
        protected override void InternalSelect(Row ARow)
        {
            FSourceTable.Select(ARow);
        }
        
        protected override bool InternalNext()
        {
			if (FSourceTable.Next())
			{
				FBOF = false;
				return true;
			}
			FBOF = FSourceTable.BOF();
			return false;
        }
        
        protected override bool InternalBOF()
        {
			return FBOF;
        }
        
        protected override bool InternalEOF()
        {
			if (FBOF)
			{
				InternalNext();
				if (FSourceTable.EOF())
					return true;
				else
				{
					if (FSourceTable.Supports(CursorCapability.BackwardsNavigable))
						FSourceTable.First();
					else
						FSourceTable.Reset();
					FBOF = true;
					return false;
				}
			}
			return FSourceTable.EOF();
        }
    }
}