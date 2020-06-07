using System;
using System.Linq;
using System.Xml.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using DVu.Library.PersistenceInterface;
using DVu.Library.PersistenceLayer;
using DVu.Library.Utility;

namespace DVu.Library.BusinessObject
{
    [Serializable]
    public abstract class ATransactionPersistenceObject
    {
        internal abstract void Commit();

        internal abstract void Refresh();

        internal abstract bool IsDelayedCommit { get; set; }

        private int _executionSequence;

        internal int ExecutionSequence
        {
            get { return _executionSequence; }
            set { _executionSequence = value; }
        }
    }

    [Serializable]
    public abstract class APersistenceObject : ATransactionPersistenceObject, IPersistenceObject
	{
        private APersistenceObjectState _state;
        private APersistenceObjectState _previousState;
        private bool _isDelayedCommit;
        private object _containingCollection;
        private bool _isExistingKeyDirty;
        //protected object _sourceObject;

        protected APersistenceObject()
		{
			_state = NewState.GetInstance();
		}

        private StringCollection _keyFieldNameCollection;
        internal StringCollection KeyFieldNameCollection
        {
            get
            {
                if (_keyFieldNameCollection == null)
                {
                    var keyFieldNameCollection =
                        (from propertyInfo in ObjectKeyType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                         select propertyInfo.Name)
                        .Union
                        (from fieldInfo in ObjectKeyType.GetFields(BindingFlags.Public | BindingFlags.Instance)
                         select fieldInfo.Name);
                    _keyFieldNameCollection = new StringCollection();
                    foreach (var keyFieldName in keyFieldNameCollection)
                        _keyFieldNameCollection.Add(keyFieldName);
                }
                return _keyFieldNameCollection;
            }
        }

        public XElement ToXml()
        {
            var type = GetType();
            var parentType = Parent == null ? null : Parent.GetType();
            var containingCollectionType = _containingCollection == null ? null : _containingCollection.GetType();
            var root = new XElement(type.Name,
                (from propertyInfo in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 let toXmlMethod = propertyInfo.PropertyType.GetMethod("ToXml")
                 let toXmlMethodWithRootName = propertyInfo.PropertyType.GetMethod("ToXml", new[] { typeof(string) } )
                 let propertyValue = propertyInfo.GetValue(this, null)
                 let propertyValueType = propertyValue == null ? null : propertyValue.GetType()
                 where propertyValue != null
                    && propertyValueType != parentType
                    && propertyValueType != containingCollectionType
                    && !(propertyValue is Type)
                    && propertyInfo.GetCustomAttributes(typeof(NonXmlSerializedAttribute), true).Length == 0
                 select toXmlMethod != null
                    ? toXmlMethodWithRootName != null 
                        ? toXmlMethod.Invoke(propertyValue, new object[] { propertyInfo.Name })
                        : toXmlMethod.Invoke(propertyValue, null)
                    : new XAttribute(propertyInfo.Name, propertyValue))
                .Union
                (from fieldInfo in type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                 let toXmlMethod = fieldInfo.FieldType.GetMethod("ToXml")
                 let toXmlMethodWithRootName = fieldInfo.FieldType.GetMethod("ToXml", new[] { typeof(string) })
                 let fieldValue = fieldInfo.GetValue(this)
                 let fieldValueType = fieldValue == null ? null : fieldValue.GetType()
                 where fieldValue != null
                    && fieldValueType != parentType
                    && fieldValueType != containingCollectionType
                    && !(fieldValue is Type)
                    && fieldInfo.GetCustomAttributes(typeof(NonXmlSerializedAttribute), true).Length == 0
                 select toXmlMethod != null
                    ? toXmlMethodWithRootName != null
                        ? toXmlMethod.Invoke(fieldValue, new object[] { fieldInfo.Name })
                        : toXmlMethod.Invoke(fieldValue, null)
                    : new XAttribute(fieldInfo.Name, fieldValue)));
            return root;
        }

        public abstract Type ObjectKeyType { get; }

        private AObjectKey _objectKey;
        [NonXmlSerialized, NotMapped]
        public IObjectKey ObjectKey 
        {
            get
            {
                if (_isExistingKeyDirty)
                    return GetObjectKeyFromFields();
                return _objectKey ?? (_objectKey = (AObjectKey)Activator.CreateInstance(ObjectKeyType));
            }
            set
            {
                _objectKey = (AObjectKey)value;
                // assign key values to object fields
                var gu = GeneralUtility.GetInstance();
                foreach (var keyFieldName in KeyFieldNameCollection)
                    gu.SetMemberValue(this, keyFieldName, gu.GetMemberValue(_objectKey, keyFieldName));
            }
        }

        internal AObjectKey GetObjectKeyFromFields()
        {
            var objectKey = (AObjectKey)Activator.CreateInstance(ObjectKeyType);
            var gu = GeneralUtility.GetInstance();
            foreach (var keyFieldName in KeyFieldNameCollection)
                gu.SetMemberValue(objectKey, keyFieldName, gu.GetMemberValue(this, keyFieldName));
            return objectKey;
        }

        protected void FieldDataChange(string fieldName, object newValue)
        {
            if (IsKeyField(fieldName))
            {
                var gu = GeneralUtility.GetInstance();

                if (Parent != null)
                    if (Parent.KeyFieldNameCollection.Cast<string>().Any(
                            parentKeyFieldName => fieldName == parentKeyFieldName &&
                                                  String.CompareOrdinal(
                                                      gu.GetMemberValue(Parent.ObjectKey, parentKeyFieldName).ToString(),
                                                      newValue.ToString()) != 0))
                        throw new ApplicationException("Invalid key values: parent - child keys mismatched.");

                // have to do the check before the assignment below
                var allKeyFieldHasValue = ObjectKey.ToDictionary().Cast<DictionaryEntry>().All(item => item.Value != null && item.Value.ToString() != "Null");
                if (allKeyFieldHasValue && !(_state is NewState))
                    _isExistingKeyDirty = true;
                    
                // if ObjectKey field == null copy newValue to it
                // so it's only initialize and no update
                var keyFieldValue = gu.GetMemberValue(ObjectKey, fieldName);
                if (keyFieldValue == null || keyFieldValue.ToString() == "Null")
                    gu.SetMemberValue(ObjectKey, fieldName, newValue);

                if (_isExistingKeyDirty)
                    ContainingCollection.GetType().GetMethod("Replace", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(ContainingCollection, new object[] { _objectKey, this });
            }
            _state.FieldDataChange(this);
        }

        private bool IsKeyField(string fieldName)
        {
            return ObjectKeyType.GetMember(fieldName).Any();
        }

        #region IPersistenceObject Members
        public virtual void Load()
        {
            PersistenceFacade.GetInstance().Get(this);
            MarkOldClean();
        }

        public virtual void Load(ObjectMapperType objectMapperType)
        {
            PersistenceFacade.GetInstance().Get(this, objectMapperType);
            MarkOldClean();
        }

        public virtual void LoadFromObject(object sourceObject)
        {
            PersistenceFacade.GetInstance().GetFromObject(this, sourceObject);
            MarkNew();
        }

        public virtual bool Validate()
        {
            return true;
        }

        public virtual void Save()
        {
            try
            {
                _isExistingKeyDirty = false;
                _state.Save(this);
                Commit();
            }
            catch (Exception)
            {
                Refresh();
                throw;
            }
        }

        public virtual void Delete()
        {
            try
            {
                _state.Delete(this);
                Commit();
            }
            catch (Exception)
            {
                Refresh();
                throw;
            }
        }

        [NotMapped]
        public APersistenceObject Parent { get; set; }
        #endregion

        [NotMapped]
        public object ContainingCollection
        {
            get { return _containingCollection; }
            set { _containingCollection = value; }
        }

        #region ATransactionPersistenceObject Members
        internal override void Commit()
        {
            if (_isDelayedCommit)
                return;
            _state.Commit(this);
        }

        internal override void Refresh()
        {
            _state.Refresh(this);
        }

        internal override bool IsDelayedCommit
        {
            get { return _isDelayedCommit; }
            set { _isDelayedCommit = value; }
        }
        #endregion

        internal APersistenceObjectState State
        {
            get { return _state; }
            set
            {
                _previousState = _state;
                _state = value;
            }
        }

        internal APersistenceObjectState PreviousState
        {
            get { return _previousState; }
            set { _previousState = value; }
        }

        internal void MarkNew()
        {
            _previousState = _state;
            _state = NewState.GetInstance();
        }

        internal void MarkDelete()
        {
            _previousState = _state;
            _state = OldDeleteState.GetInstance();
        }

        internal void MarkOldClean()
        {
            _previousState = _state;
            _state = OldCleanState.GetInstance();
        }

        internal void MarkOldDirty()
        {
            _previousState = _state;
            _state = OldDirtyState.GetInstance();
        }
	}

    public enum SortDirection { Ascending, Descending }

    [Serializable]
    public abstract class APersistenceObjectCollection<TKey, TPersistenceObject> : ATransactionPersistenceObject, IPersistenceObjectCollection, IList<TPersistenceObject>
        where TPersistenceObject : APersistenceObject, new()
        where TKey : AObjectKey, new()
    {
        // need to implement:
        //   Equals, GetHashcode, IsSynchronized and SyncRoot
        private List<TPersistenceObject> _arrayObjects;
        private Dictionary<string, int> _hashObjects;
        private readonly Dictionary<string, TPersistenceObject> _deletedObjects;
        protected HybridDictionary ParameterCollection;
        private bool _isDelayedCommit;
        private HybridDictionary _comparers;

        [NotMapped]
        public Type ObjectKeyType
        {
            get { return Parent == null ? null : Parent.ObjectKeyType; }
        }

        [NotMapped]
        public IObjectKey ObjectKey
        {
            get { return Parent == null ? null : Parent.ObjectKey; }
            set
            {
                if (Parent != null)
                    Parent.ObjectKey = value;
            }
        }

        protected APersistenceObjectCollection()
        {
            _arrayObjects = new List<TPersistenceObject>();
            _hashObjects = new Dictionary<string, int>();
            _deletedObjects = new Dictionary<string, TPersistenceObject>();
        }

        //private void GetInheritProperties(Type type, ref ArrayList pis)
        //{
        //    pis.AddRange(type.GetProperties());
        //    foreach (Type baseType in type.GetInterfaces())
        //        GetInheritProperties(baseType, ref pis);
        //}

        private static void GetBaseNestedTypes(Type type, ref ArrayList nestedTypes)
        {
            nestedTypes.AddRange(type.GetNestedTypes(BindingFlags.NonPublic));
            if (type.BaseType != null)
                GetBaseNestedTypes(type.BaseType, ref nestedTypes);
        }

        protected HybridDictionary Comparers
        {
            get
            {
                if (_comparers == null)
                {
                    _comparers = new HybridDictionary();
                    var type = GetType();
                    var comparerTypes = new ArrayList();
                    GetBaseNestedTypes(type, ref comparerTypes);
                    foreach (var comparerType in comparerTypes.Cast<Type>().Where(comparerType => comparerType.Name.IndexOf("comparer_", StringComparison.Ordinal) > -1))
                        _comparers[comparerType.Name.Replace("comparer_", string.Empty)] = Activator.CreateInstance(comparerType);
                }
                return _comparers;
            }
        }

        public XElement ToXml(string elementName)
        {
            var element = new XElement(elementName,
                from obj in _arrayObjects
                select obj.ToXml());
            return element;
        }

        public void MarkNew()
        {
            foreach (var obj in _arrayObjects)
                obj.MarkNew();
        }

        protected void LoadSetVersion()
        {
            var setVersion = this as ISetVersion;
            if (setVersion != null)
            {
                var setHeader = setVersion.SetHeader as IPersistenceObject;
                if (setHeader != null)
                    setHeader.Load();
            }
        }

        protected void IncrementSetVersion()
        {
            var setVersion = this as ISetVersion;
            if (setVersion == null)
                return;
            var setHeader = setVersion.SetHeader as APersistenceObject;
            if (setHeader == null)
                return;
            if (setVersion.SetHeader.RowVersion == null)
                setHeader.MarkNew();
            else
                setHeader.MarkOldDirty();
            setHeader.Save();
        }

        #region IPersistenceObjectCollection Members
        public virtual void Load()
        {
            PersistenceFacade.GetInstance().Get(this, ParameterCollection);
            LoadSetVersion();
        }

        public void Load(ObjectMapperType objectMapperType)
        {
            throw new NotImplementedException();
        }

        public void LoadFromObject(object sourceObject)
        {   
            throw new NotImplementedException();
        }

        public virtual bool Validate()
        {
            return _arrayObjects.All(obj => obj.Validate());
        }

        public virtual void Save()
        {
            IncrementSetVersion();
            foreach (var obj in _arrayObjects)
                obj.Save();
        }

        public virtual void Save(TPersistenceObject obj)
        {
            try
            {
                IncrementSetVersion();
            }
            catch
            {
                obj.Load();
                throw;
            }
            obj.Save();
            Add(obj);
        }

        public virtual void Delete()
        {
            IncrementSetVersion();
            while (_arrayObjects.Count > 0)
            {
                var obj = _arrayObjects[0];
                if (obj == null) 
                    continue;
                obj.Delete();
                Remove((TKey)obj.ObjectKey);
            }
        }

        public virtual void Delete(TKey objectKey)
        {
            try
            {
                IncrementSetVersion();
            }
            catch
            {
                var obj = _arrayObjects[_hashObjects[objectKey.ToString()]];
                if (obj != null)
                    obj.Load();
                throw;
            }
            if (_hashObjects.ContainsKey(objectKey.ToString()))
            {
                var obj = _arrayObjects[_hashObjects[objectKey.ToString()]];
                if (obj != null)
                {
                    obj.Delete();
                    Remove(objectKey);
                }
            }
        }

        [NotMapped]
        public APersistenceObject Parent { get; set; }

        /// <summary>
        /// Create a new object for data retrieval. This is for internal use only.
        /// </summary>
        public IPersistenceObject CreateObjectForRetrieval()
        {
            return new TPersistenceObject();
        }

        /// <summary>
        /// Initializes the collection for data retrieval. This is for internal use only.
        /// </summary>
        /// <param name="capacity">The intial collection capacity</param>
        public void Initialize(int capacity)
        {
            _arrayObjects = new List<TPersistenceObject>(capacity);
            _hashObjects = new Dictionary<string, int>(capacity);
            _deletedObjects.Clear();
        }

        /// <summary>
        /// Adds object just retrieved from data store to the collection. This is for internal use only.
        /// </summary>
        /// <param name="persistenceObject">The persistenceObject to be added</param>
        public void AddRetrievedObject(IPersistenceObject persistenceObject)
        {
            var obj = (TPersistenceObject)persistenceObject;
            obj.MarkOldClean();
            Add(obj);
        }
        #endregion

        #region ATransactionPersistenceObject Members
        internal override void Commit()
        {
            if (_isDelayedCommit)
                return;
            foreach (var obj in _deletedObjects)
                obj.Value.Commit();
            foreach (var obj in _arrayObjects)
                obj.Commit();
            _deletedObjects.Clear();
        }

        internal override void Refresh()
        {
            Load();
        }

        internal override bool IsDelayedCommit
        {
            get { return _isDelayedCommit; }
            set
            {
                foreach (var obj in _deletedObjects)
                    obj.Value.IsDelayedCommit = value;
                foreach (var obj in _arrayObjects)
                    obj.IsDelayedCommit = value;
                _isDelayedCommit = value;
            }
        }
        #endregion

        #region Public Collection Feature Implementation
        [NotMapped]
        public int Count
        {
            get { return _arrayObjects.Count; }
        }

        public void Clear()
        {
            foreach (var obj in _arrayObjects.Where(obj => !_deletedObjects.ContainsKey(obj.ObjectKey.ToString())))
            {
                obj.MarkDelete();
                _deletedObjects.Add(obj.ObjectKey.ToString(), obj);
            }
            _arrayObjects.Clear();
            _hashObjects.Clear();
        }

        public bool ContainsKey(TKey objectKey)
        {
            return _hashObjects.ContainsKey(objectKey.ToString());
        }

        public bool ContainsValue(TPersistenceObject value)
        {
            return _hashObjects.ContainsValue(IndexOf(value));
        }

        public bool Contains(TPersistenceObject persistenceObject)
        {
            return _arrayObjects.Contains(persistenceObject);
        }

        public void RemoveAt(int index)
        {
            var obj = this[index];
            if (obj != null)
                Remove(obj);
        }

        private void RebuildHashObjects(int startIndex)
        {
            // rebuild the hashObjects when the arrayObjects indexes change
            if (startIndex == 0)
                _hashObjects = new Dictionary<string, int>(_arrayObjects.Count);
            for (var index = startIndex; index < _arrayObjects.Count; index++)
                _hashObjects[_arrayObjects[index].ObjectKey.ToString()] = index;
        }

        public void Reverse()
        {
            _arrayObjects.Reverse();
            RebuildHashObjects(0);
        }

        public void Reverse(int index, int count)
        {
            _arrayObjects.Reverse(index, count);
            RebuildHashObjects(index);
        }

        public void RemoveRange(int index, int count)
        {
            for (var i = index; i < index + count; i++)
            {
                //this.RemoveAt(index);
                var obj = _arrayObjects[i];
                if (_arrayObjects.Contains(obj))
                    _arrayObjects.Remove(obj);
                else
                    foreach (var pObj in _arrayObjects.Where(pObj => pObj.ObjectKey.ToString() == obj.ObjectKey.ToString()))
                    {
                        _arrayObjects.Remove(pObj);
                        break;
                    }
                _hashObjects.Remove(obj.ObjectKey.ToString());
                if (!_deletedObjects.ContainsKey(obj.ObjectKey.ToString()))
                {
                    obj.MarkDelete();
                    _deletedObjects.Add(obj.ObjectKey.ToString(), obj);
                }
            }
            RebuildHashObjects(index);
        }

        public void Sort(String sortExpression, SortDirection sortDirection)
        {
            if (Comparers != null)
            {
                _arrayObjects.Sort((IComparer<TPersistenceObject>)Comparers[sortExpression]);
                if (sortDirection == SortDirection.Descending)
                    _arrayObjects.Reverse();
                RebuildHashObjects(0);
            }
        }

        public void Sort(IComparer<TPersistenceObject> comparer)
        {
            _arrayObjects.Sort(comparer);
            RebuildHashObjects(0);
        }

        public void Sort(int index, int count, IComparer<TPersistenceObject> comparer)
        {
            _arrayObjects.Sort(index, count, comparer);
            RebuildHashObjects(index);
        }

        public void Remove(TKey objectKey)
        {
            var obj = this[objectKey];
            if (obj != null)
                Remove(obj);
        }

        public virtual TPersistenceObject Create(TKey objectKey)
        {
            return this[objectKey] ?? new TPersistenceObject {ObjectKey = objectKey};
        }

        protected void Replace(TKey oldKey, TPersistenceObject newObj)
        {
            var oldObj = this[oldKey];
            if (oldObj != null)
            {
                if (_arrayObjects.Contains(oldObj))
                    _arrayObjects.Remove(oldObj);
                else
                    foreach (var pObj in _arrayObjects.Where(pObj => pObj.ObjectKey.ToString() == oldKey.ToString()))
                    {
                        _arrayObjects.Remove(pObj);
                        break;
                    }
                var startIndex = _hashObjects[oldKey.ToString()];
                _hashObjects.Remove(oldKey.ToString());
                RebuildHashObjects(startIndex);
            }

            Add(newObj);
        }

        public virtual void Add(TPersistenceObject obj)
        {
            if (_hashObjects.ContainsKey(obj.ObjectKey.ToString()))
                //throw new ApplicationException("Item already exists in collection.");
                return;
            _arrayObjects.Add(obj);
            _hashObjects.Add(obj.ObjectKey.ToString(), _arrayObjects.Count - 1);
            obj.ContainingCollection = this;
        }

        public void Insert(int index, TPersistenceObject obj)
        {
            if (_hashObjects.ContainsKey(obj.ObjectKey.ToString()))
                return;
            obj.MarkNew();
            _arrayObjects.Insert(index, obj);
            // do not need to add below, the RebuildHashObjects will do it
            //hashObjects.Add(obj.ObjectKey.ToString(), index);
            RebuildHashObjects(index);
        }

        public void InsertRange(int index, APersistenceObjectCollection<TKey, TPersistenceObject> collection)
        {
            for (var i = collection.Count - 1; i > -1; i--)
            {
                var obj = _arrayObjects[i];
                if (_hashObjects.ContainsKey(obj.ObjectKey.ToString()))
                    continue;
                obj.MarkNew();
                _arrayObjects.Insert(index, obj);
                // do not need to add below, the RebuildHashObjects will do it
                //hashObjects.Add(obj.ObjectKey.ToString(), index);
            }
            RebuildHashObjects(index);
        }

        public void CopyTo(TPersistenceObject[] array, int arrayIndex)
        {
            _arrayObjects.CopyTo(array, arrayIndex);
        }

        public bool Remove(TPersistenceObject obj)
        {
            var result = false;
            if (_arrayObjects.Contains(obj))
                result = _arrayObjects.Remove(obj);
            else
                foreach (var pObj in _arrayObjects.Where(pObj => pObj.ObjectKey.ToString() == obj.ObjectKey.ToString()))
                {
                    result = _arrayObjects.Remove(pObj);
                    break;
                }
            var startIndex = _hashObjects[obj.ObjectKey.ToString()];
            _hashObjects.Remove(obj.ObjectKey.ToString());
            if (!_deletedObjects.ContainsKey(obj.ObjectKey.ToString()))
            {
                obj.MarkDelete();
                _deletedObjects.Add(obj.ObjectKey.ToString(), obj);
            }
            RebuildHashObjects(startIndex);
            return result;
        }

        public int IndexOf(TPersistenceObject value)
        {
            return _arrayObjects.IndexOf(value);
        }

        public int IndexOf(TPersistenceObject value, int startIndex)
        {
            return _arrayObjects.IndexOf(value, startIndex);
        }

        public int IndexOf(TPersistenceObject value, int startIndex, int count)
        {
            return _arrayObjects.IndexOf(value, startIndex, count);
        }

        public int LastIndexOf(TPersistenceObject value)
        {
            return _arrayObjects.LastIndexOf(value);
        }

        public int LastIndexOf(TPersistenceObject value, int startIndex)
        {
            return _arrayObjects.LastIndexOf(value, startIndex);
        }

        public int LastIndexOf(TPersistenceObject value, int startIndex, int count)
        {
            return _arrayObjects.LastIndexOf(value, startIndex, count);
        }

        [NotMapped]
        public TPersistenceObject this[TKey objectKey]
        {
            get
            {
                if (_hashObjects.ContainsKey(objectKey.ToString()))
                {
                    var obj = _arrayObjects[_hashObjects[objectKey.ToString()]];
                    //if (obj.Parent == null)
                    //    obj.Parent = this.Parent;
                    return obj;
                }
                return null;
            }
            set
            {
                if (_hashObjects.ContainsKey(objectKey.ToString()))
                    _arrayObjects[_hashObjects[objectKey.ToString()]] = value;
                else
                    Add(value);
            }
        }

        [NotMapped]
        public TPersistenceObject this[int index]
        {
            get { return _arrayObjects[index]; }
            set { _arrayObjects[index] = value; }
        }

        [NotMapped]
        public bool IsReadOnly
        {
            get { return false; }
        }
        #endregion

        #region IEnumerable Interface Implementation
        public virtual IEnumerator<TPersistenceObject> GetEnumerator()
        {
            return _arrayObjects.Select((t, i) => this[i]).GetEnumerator();
        }


        IEnumerator IEnumerable.GetEnumerator() // IEnumerable version
        {
            return GetEnumerator();
        }

        //// IEnumerable Interface Implementation:
        //public virtual PersistenceObjectEnumerator GetEnumerator() // non-IEnumerable version
        //{
        //    return new PersistenceObjectEnumerator(this);
        //}

        //IEnumerator IEnumerable.GetEnumerator() // IEnumerable version
        //{
        //    return (IEnumerator)new PersistenceObjectEnumerator(this);
        //}

        //// Inner class implements IEnumerator interface:
        //public class PersistenceObjectEnumerator : IEnumerator
        //{
        //    protected int position = -1;
        //    protected APersistenceObjectCollection<TKey, TAPersistenceObject> collection;

        //    public PersistenceObjectEnumerator(APersistenceObjectCollection<TKey, TAPersistenceObject> collection)
        //    {
        //        this.collection = collection;
        //    }

        //    public bool MoveNext()
        //    {
        //        if (position < collection.Count - 1)
        //        {
        //            position++;
        //            return true;
        //        }
        //        else
        //        {
        //            return false;
        //        }
        //    }

        //    public void Reset()
        //    {
        //        position = -1;
        //    }

        //    public virtual TAPersistenceObject Current // non-IEnumerator version: type-safe
        //    {
        //        get { return collection.arrayObjects[position]; }
        //    }

        //    object IEnumerator.Current // IEnumerator version: returns object
        //    {
        //        get { return collection.arrayObjects[position]; }
        //    }
        //}
        #endregion
    }

    [Serializable]
    public abstract class AObjectKey : IObjectKey
    {
        public abstract override string ToString();
        
        //public override string ToString()
        //{
        //    Type type = this.GetType();
        //    var fieldCollection =
        //        (from propertyInfo in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        //         orderby propertyInfo.Name
        //         select propertyInfo.GetValue(this, null))
        //        .Union
        //        (from fieldInfo in type.GetFields(BindingFlags.Public | BindingFlags.Instance)
        //         orderby fieldInfo.Name
        //         select fieldInfo.GetValue(this));
        //    System.Text.StringBuilder sb = new System.Text.StringBuilder();
        //    foreach (var fieldValue in fieldCollection)
        //        sb.Append("|").Append(fieldValue);
        //    return sb.ToString(1, sb.Length - 1);
        //}

        public virtual XElement ToXml()
        {
            var type = GetType();
            var objectKey = new XElement("ObjectKey",
                (from propertyInfo in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 where propertyInfo.GetValue(this, null).ToString() != "Null"
                 select new XAttribute(propertyInfo.Name, propertyInfo.GetValue(this, null)))
                .Union
                (from fieldInfo in type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                 where fieldInfo.GetValue(this).ToString() != "Null"
                 select new XAttribute(fieldInfo.Name, fieldInfo.GetValue(this))));
            return objectKey;
        }

        public virtual HybridDictionary ToDictionary()
        {
            var type = GetType();
            var fieldCollection = (
                (from propertyInfo in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                 let Value = propertyInfo.GetValue(this, null)
                 select new { propertyInfo.Name, Value })
                .Union
                (from fieldInfo in type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                 let Value = fieldInfo.GetValue(this)
                 select new { fieldInfo.Name, Value }) ).ToList();
            var dictionary = new HybridDictionary(fieldCollection.Count());
            foreach (var field in fieldCollection)
                dictionary.Add(field.Name, field.Value);
            return dictionary;
        }
    }

    [Serializable]
    public abstract class APersistenceObjectWithParentCollection<TKey, TPersistenceObject> : APersistenceObjectCollection<TKey, TPersistenceObject>
        where TPersistenceObject : APersistenceObject, new()
        where TKey : AObjectKey, new()
    {
        private bool ValidateParentChildKey(TPersistenceObject obj)
        {
            var gu = GeneralUtility.GetInstance();
            return obj.Parent.KeyFieldNameCollection.Cast<string>().All(parentKeyFieldName => String.CompareOrdinal(gu.GetMemberValue(Parent.ObjectKey, parentKeyFieldName).ToString(), gu.GetMemberValue(obj.ObjectKey, parentKeyFieldName).ToString()) == 0);
        }
        
        public override void Add(TPersistenceObject obj)
        {
            if (Parent == null)
                throw new ApplicationException("Collection requires a parent assigned before new objects can be added.");
            if (obj.Parent == null)
                obj.Parent = Parent;
            if (!ValidateParentChildKey(obj))
                throw new ApplicationException("Invalid key values: parent - child keys mismatched.");
            base.Add(obj);
        }

        public override void Load()
        {
            if (ParameterCollection != null)
                PersistenceFacade.GetInstance().Get(this, Parent.ObjectKey, ParameterCollection);
            else
                PersistenceFacade.GetInstance().Get(this, Parent.ObjectKey);
            LoadSetVersion();
        }
    }
}