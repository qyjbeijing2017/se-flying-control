using System;
using System.Collections.Generic;
using System.Linq;

namespace IngameScript
{
    public class ParameterParser
    {
        private Dictionary<string, string> _parameters;

        /// <summary>
        /// 初始化参数解析器，格式: key1=value1,key2=value2,key3=value3
        /// </summary>
        public ParameterParser(string parameterString)
        {
            _parameters = new Dictionary<string, string>();
            Parse(parameterString);
        }

        /// <summary>
        /// 解析参数字符串，支持多种分隔符: , | \n
        /// </summary>
        private void Parse(string parameterString)
        {
            if (string.IsNullOrWhiteSpace(parameterString))
                return;

            // 按照逗号、竖线或换行符分割参数
            var parameters = parameterString.Split(new[] { ',', '|', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var parameter in parameters)
            {
                var trimmedParam = parameter.Trim();
                
                // 按等号分割键和值
                var parts = trimmedParam.Split('=');
                
                if (parts.Length == 2)
                {
                    var key = parts[0].Trim();
                    var value = parts[1].Trim();
                    _parameters[key] = value;
                }
            }
        }

        /// <summary>
        /// 获取参数值（如果不存在则返回默认值）
        /// </summary>
        public string Get(string key, string defaultValue = "")
        {
            return _parameters.ContainsKey(key) ? _parameters[key] : defaultValue;
        }

        /// <summary>
        /// 获取参数值并转换为整数
        /// </summary>
        public int GetInt(string key, int defaultValue = 0)
        {
            int result;
            if (_parameters.ContainsKey(key) && int.TryParse(_parameters[key], out result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// 获取参数值并转换为浮点数
        /// </summary>
        public float GetFloat(string key, float defaultValue = 0f)
        {
            float result;
            if (_parameters.ContainsKey(key) && float.TryParse(_parameters[key], out result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// 获取参数值并转换为布尔值
        /// </summary>
        public bool GetBool(string key, bool defaultValue = false)
        {
            bool result;
            if (_parameters.ContainsKey(key) && bool.TryParse(_parameters[key], out result))
                return result;
            return defaultValue;
        }

        /// <summary>
        /// 检查参数是否存在
        /// </summary>
        public bool HasParameter(string key)
        {
            return _parameters.ContainsKey(key);
        }

        /// <summary>
        /// 获取所有参数字典
        /// </summary>
        public Dictionary<string, string> GetAll()
        {
            return new Dictionary<string, string>(_parameters);
        }
    }
}
