using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;

namespace DVu.Library.Utility
{
    public sealed class GeneralUtility
    {
        private static volatile GeneralUtility _instance;
        private static readonly object SyncRoot = new object();

        private GeneralUtility() { }

        public static GeneralUtility GetInstance()
        {
            if (_instance == null)
                lock (SyncRoot)
                {
                    if (_instance == null)
                        _instance = new GeneralUtility();
                }
            return _instance;
        }

        public object GetStaticMemberValue(string variableName, string assemblyName)
        {
            object memberObject = null;
            int[] arrayIndexes = null;
            object arrayElementObject = null;
            var memberNameParts = variableName.Split('.');
            var typeName = string.Empty;
            for (var i = 0; i < memberNameParts.Length; i++)
            {
                var memberNamePart = Regex.Replace(memberNameParts[i], "/( */)", string.Empty);
                typeName = typeName == string.Empty ? memberNamePart : string.Format("{0}.{1}", typeName, memberNamePart);
                var type = Type.GetType(assemblyName != string.Empty ? string.Format("{0},{1}", typeName, assemblyName) : typeName);
                if (type != null)
                {
                    for (var j = i + 1; j < memberNameParts.Length; j++)
                    {
                        MemberInfo memberInfo;
                        memberObject = GetMemberInfo(type, memberObject, memberNameParts[j].Replace("()", string.Empty), out memberInfo, out arrayIndexes, out arrayElementObject);
                    }
                    break;
                }
            }
            return arrayIndexes != null ? arrayElementObject : memberObject;
        }

        public object GetMemberValue(object containingObject, string variableName)
        {
            var memberObject = containingObject;
            MemberInfo memberInfo;
            var memberNameParts = variableName.Split('.');
            int[] arrayIndexes = null;
            object arrayElementObject = null;
            for (var i = 0; i < memberNameParts.Length && memberObject != null; i++)
                memberObject = GetMemberInfo(memberObject.GetType(), memberObject, memberNameParts[i], out memberInfo, out arrayIndexes, out arrayElementObject);
            return arrayIndexes != null ? arrayElementObject : memberObject;
        }

        public void SetMemberValue(object containingObject, string memberName, object value)
        {
            var memberObject = containingObject;
            var parentObject = containingObject;
            MemberInfo memberInfo = null;
            var memberNameParts = memberName.Split('.');
            int[] arrayIndexes = null;
            for (var i = 0; i < memberNameParts.Length && memberObject != null; i++)
            {
                object arrayElementObject;
                parentObject = memberObject;
                memberObject = GetMemberInfo(memberObject.GetType(), memberObject, memberNameParts[i], out memberInfo, out arrayIndexes, out arrayElementObject);
            }

            if (memberInfo == null)
                throw new ApplicationException(string.Format("Member: {0} not found for type: {1}", memberName, parentObject.GetType().FullName));

            if (memberInfo.MemberType == MemberTypes.Field)
            {
                var fieldInfo = (FieldInfo)memberInfo;
                if (!fieldInfo.IsInitOnly)
                {
                    if (fieldInfo.FieldType.IsArray && arrayIndexes != null)
                    {
                        if (memberObject != null)
                            ((Array)memberObject).SetValue(value, arrayIndexes);
                    }
                    else
                    {
                        if (value is DBNull || value == null)
                        {
                            if (fieldInfo.FieldType is System.Data.SqlTypes.INullable)
                                // assign INullable Null
                                fieldInfo.SetValue(parentObject, fieldInfo.FieldType.GetField("Null").GetValue(null));
                            else
                                // assign CLR null
                                fieldInfo.SetValue(parentObject, null);
                        }
                        else
                        {
                            var valueType = value.GetType();
                            if (fieldInfo.FieldType.IsAssignableFrom(valueType) ||
                                (fieldInfo.FieldType.IsEnum && valueType.IsEnum))
                                // source can be assigned to target then straight assignment
                                fieldInfo.SetValue(parentObject, value);
                            else // create new instance with value passed into the constructor and assign to target
                                fieldInfo.SetValue(parentObject, Activator.CreateInstance(fieldInfo.FieldType, value));
                        }
                    }
                }
            }
            else if (memberInfo.MemberType == MemberTypes.Property)
            {
                var propertyInfo = (PropertyInfo)memberInfo;
                if (propertyInfo.CanWrite)
                {
                    if (propertyInfo.PropertyType.IsArray && arrayIndexes != null)
                    {
                        if (memberObject != null)
                            ((Array)memberObject).SetValue(value, arrayIndexes);
                    }
                    else
                    {
                        if (value is DBNull || value == null)
                        {
                            if (propertyInfo.PropertyType is System.Data.SqlTypes.INullable)
                                // assign INullable Null
                                propertyInfo.SetValue(parentObject, propertyInfo.PropertyType.GetField("Null").GetValue(null), null);
                            else
                                // assign CLR null
                                propertyInfo.SetValue(parentObject, null, null);
                        }
                        else
                        {
                            var valueType = value.GetType();
                            if (propertyInfo.PropertyType.IsAssignableFrom(valueType) ||
                                (propertyInfo.PropertyType.IsEnum && valueType.IsEnum))
                                // source can be assigned to target then straight assignment
                                propertyInfo.SetValue(parentObject, value, null);
                            else // create new instance with value passed into the constructor and assign to target
                                propertyInfo.SetValue(parentObject, Activator.CreateInstance(propertyInfo.PropertyType, value), null);
                        }
                    }
                }
            }
        }

        private static object GetMemberInfo(Type type, object typeInstance, string memberName, out MemberInfo memberInfo, out int[] arrayIndexes, out object arrayElementObject)
        {
            /*
            aB_ba0[01]
            AB_a[01][9]
            aB0[0]
            ab[0 , 1]
            AB_a[01, 2][9]
            ABc[]
            abC
            a_
            a[0]
            a_[1]
            a0[2]
            a__o[0]
            */
            memberInfo = null;
            arrayIndexes = null;
            arrayElementObject = null;
            
            // probably want to check name pattern in a tool to validate the xml
            // performance penalty to do it here on every operation
            //if (!Regex.IsMatch(memberName, @"^[A-Za-z]+[A-Za-z0-9]+(?:_[A-Za-z0-9]+)?$|^[A-Za-z]+[A-Za-z0-9]+(?:_[A-Za-z0-9]+)? ?(?:\[ ?\d+ ?(?:, ?\d+)? ?\])+ ?$"))
            //    throw new ApplicationException(string.Format("Unexpected Member Name patern for {0}. Supported name patterns: MemberName.MemberName, MemberName[0][1], MemberName[0, 1]...", memberName));
            
            var memberNameElements = memberName.Split(new[] {'[', ']', ','});

            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            object memberObject = null;

            var memberInfos = type.GetMember(memberNameElements[0], bf);
            if (memberInfos.Length > 0)
            {
                memberInfo = memberInfos[0];
                if (memberInfo.MemberType == MemberTypes.Field)
                    memberObject = ((FieldInfo)memberInfo).GetValue(typeInstance);
                else if (memberInfo.MemberType == MemberTypes.Property)
                    memberObject = ((PropertyInfo)memberInfo).GetValue(typeInstance, null);
                else if (memberInfo.MemberType == MemberTypes.Method)
                    memberObject = ((MethodInfo)memberInfo).Invoke(typeInstance, null);
                if (memberNameElements.Length > 1 && memberObject != null && memberObject.GetType().IsArray)
                {
                    arrayIndexes = (from memberNameElement in memberNameElements.Skip(1)
                                    where memberNameElement != string.Empty
                                    select int.Parse(memberNameElement)).ToArray();
                    arrayElementObject = ((Array)memberObject).GetValue(arrayIndexes);
                }
            }
            else if (memberNameElements.Length > 1 && type.IsArray)
            {
                arrayIndexes = (from memberNameElement in memberNameElements.Skip(1)
                                where memberNameElement != string.Empty
                                select int.Parse(memberNameElement)).ToArray();
                arrayElementObject = ((Array)typeInstance).GetValue(arrayIndexes);
            }
            return memberObject;
        }

        public Type GetMemberType(Type type, object typeInstance, string memberName)
        {
            Type memberType = null;
            const BindingFlags bf = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var memberInfos = type.GetMember(memberName, bf);
            if (memberInfos.Length > 0)
            {
                var memberInfo = memberInfos[0];
                if (memberInfo.MemberType == MemberTypes.Field)
                    memberType = ((FieldInfo)memberInfo).FieldType;
                else if (memberInfo.MemberType == MemberTypes.Property)
                    memberType = ((PropertyInfo)memberInfo).PropertyType;
            }
            return memberType;
        }
    }
}
