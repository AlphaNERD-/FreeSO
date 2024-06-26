﻿using System;
using System.Collections.Generic;

namespace FSO.Client.Utils
{
    /// <summary>
    /// Utility so calculations are only performed once where appropriate
    /// </summary>
    public class MathCache
    {
        private Dictionary<string, object> m_Value = new Dictionary<string, object>();

        public void Invalidate()
        {
            m_Value.Clear();
        }

        public void Invalidate(string id)
        {
            m_Value.Remove(id);
        }

        public TResult Calculate<TResult>(string id, Func<object, TResult> calculator)
        {
            if (!m_Value.ContainsKey(id))
            {
                m_Value.Add(id, calculator.Invoke(null));
            }
            return (TResult)m_Value[id];
        }
    }
}
