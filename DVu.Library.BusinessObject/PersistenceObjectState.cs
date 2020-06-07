using System;

namespace DVu.Library.BusinessObject
{
    [Serializable]
	internal abstract class APersistenceObjectState
	{
        public virtual void Commit(APersistenceObject obj) { }

        public virtual void Delete(APersistenceObject obj) { }

		public virtual void Refresh(APersistenceObject obj)
		{
            if (obj.PreviousState != null)
            {
                obj.State = obj.PreviousState;
                obj.PreviousState = null;
            }
        }

        public virtual void Save(APersistenceObject obj) { }

        public virtual void FieldDataChange(APersistenceObject obj) { }
	}
}
