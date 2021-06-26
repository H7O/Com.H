using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Reflection
{
    public static class ReflectionExtensions
    {
        private static readonly DataMapper _mapper = new();

        public static (string Name, PropertyInfo Info)[] GetCachedProperties(this Type type)
            => _mapper.GetCachedProperties(type);

        public static (string Name, PropertyInfo Info)[] GetCachedProperties(this object obj)
            => _mapper.GetCachedProperties(obj);

        public static T Map<T>(this object source)
            => _mapper.Map<T>(source);

        public static object Map(this object source, Type dstType)
            => _mapper.Map(source, dstType);

        public static T Clone<T>(this T source)
            => _mapper.Clone<T>(source);

        public static IEnumerable<T> Map<T>(this IEnumerable<object> source)
            => source==null?null:_mapper.Map<T>(source);

        public static void FillWith(
            this object destination,
            object source,
            bool skipNull = false
            )
            => _mapper.FillWith(destination, source, skipNull);

        /// <summary>
        /// Rrturns values of IDictionary after filtering them based on an IEnumerable of keys.
        /// The filter keys don't have to be of the same type as the IDictionary keys.
        /// They only need to be mappable to IDictionary keys type (i.e. can be conerted to IDicionary keys type)
        /// </summary>
        /// <typeparam name="TKey"></typeparam>
        /// <typeparam name="TValue"></typeparam>
        /// <typeparam name="TOKey"></typeparam>
        /// <param name="dictionary"></param>
        /// <param name="oFilter"></param>
        /// <returns></returns>
        public static IEnumerable<TValue> OrdinallyMappedFilteredValues<TKey, TValue, TOKey>(
            IDictionary<TKey, TValue> dictionary, IEnumerable<TOKey> oFilter)
            => oFilter.Join(dictionary, o => o.Map<TKey>(), d => d.Key, (o, d) => d.Value);


        public static IEnumerable<(string Name, PropertyInfo Info)> GetProperties(this ExpandoObject expando)
        {
            if (expando == null) throw new ArgumentNullException(nameof(expando));
            foreach (var p in expando)
            {
                yield return (p.Key, new DynamicPropertyInfo(p.Key, p.Value?.GetType() ?? typeof(string)));
            }
        }


        //public static object GetDefault(this Type type)
        //{
        //    // taken from : https://stackoverflow.com/questions/2490244/default-value-of-a-type-at-runtime
        //    // If no Type was supplied, if the Type was a reference type, or if the Type was a System.Void, return null
        //    if (type == null || !type.IsValueType || type == typeof(void)
        //        ||
        //        type.ContainsGenericParameters
        //        )
        //        return null;

        //    // If the Type is a primitive type, or if it is another publicly-visible value type (i.e. struct/enum), return a 
        //    //  default instance of the value type
        //    if (type.IsPrimitive || !type.IsNotPublic)
        //    {
        //        try
        //        {
        //            return Activator.CreateInstance(type);
        //        }
        //        catch {}
        //    }
        //    return null;

        //}

    }
}