﻿using Com.H.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Com.H.Data
{
    public static class DataExtensions
    {
        public static IDictionary<string, object> GetDataModelParameters(this object dataModel, bool descending = false)
        {
            if (dataModel == null) return null;
            Dictionary<string, object> result = new Dictionary<string, object>();
            foreach (var item in dataModel.EnsureEnumerable())
            {
                if (item == null) continue;
                if (typeof(IDictionary<string, object>).IsAssignableFrom(item.GetType()))
                {
                    foreach (var x in ((IDictionary<string, object>)item))
                    {
                        if (result.Keys.Contains(x.Key) && !descending) continue;
                        result[x.Key] = x.Value;
                    }
                    continue;
                }
                foreach(var x in ((object)item).GetType().GetProperties())
                {
                    if (result.Keys.Contains(x.Name) && !descending) continue;
                    result[x.Name] = x.GetValue(item, null);
                }
            }
            return result;
        }
    }
}
