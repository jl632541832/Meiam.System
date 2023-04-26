﻿/*
* ==============================================================================
*
* FileName: ToolsService.cs
* Created: 2020/3/26 13:31:48
* Author: Meiam
* Description: 
*
* ==============================================================================
*/
using Meiam.System.Common;
using Meiam.System.Core;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Meiam.System.Tools
{

    public interface IToolsService
    {
        void Run();
    }

    public class ToolsService: IToolsService
    {

        public void Run()
        {
            var allTables = new ToolsService().GetAllTables();

            var solutionName = "Meiam.System";

            foreach (var table in allTables)
            {
                Console.Write($"生成[{ table }]表 模型: ");
                Console.WriteLine(new ToolsService().CreateModels($"..\\..\\..\\..\\{ solutionName }.Model\\Entity", solutionName, table, ""));
                Console.Write($"生成[{ table }]表 服务: ");
                Console.WriteLine(new ToolsService().CreateServices($"..\\..\\..\\..\\{ solutionName }.Interfaces\\Service", solutionName, table));
                Console.Write($"生成[{ table }]表 接口: ");
                Console.WriteLine(new ToolsService().CreateIServices($"..\\..\\..\\..\\{ solutionName }.Interfaces\\IService", solutionName, table));
            }
        }

        #region 核心方法

        public SqlSugarClient Db = new SqlSugarClient(new ConnectionConfig()
        {
            ConnectionString = AppSettings.Configuration["DbConnection:ConnectionString"],
            DbType = (DbType)Convert.ToInt32(AppSettings.Configuration["DbConnection:DbType"]),
            IsAutoCloseConnection = true,
            InitKeyType = InitKeyType.Attribute,
            ConfigureExternalServices = new ConfigureExternalServices()
            {
                DataInfoCacheService = new RedisCache()
            },
            MoreSettings = new ConnMoreSettings()
            {
                IsAutoRemoveDataCache = true
            }
        });



        /// <summary>
        /// 根据数据库表生产Model层
        /// </summary>
        /// <param name="strPath">实体类存放路径</param>
        /// <param name="strSolutionName">项目名称</param>
        /// <param name="tableName">生产指定的表</param>
        /// <param name="strInterface">实现接口</param>
        /// <param name="blnSerializable">是否序列化</param>
        public bool CreateModels(string strPath, string strSolutionName, string tableName, string strInterface, bool blnSerializable = false)
        {

            try
            {

                #region 模板样式
                var classTemplate = $"" +
                    $"//------------------------------------------------------------------------------\r\n" +
                    $"// <auto-generated>\r\n" +
                    $"//     此代码已从模板生成手动更改此文件可能导致应用程序出现意外的行为。\r\n" +
                    $"//     如果重新生成代码，将覆盖对此文件的手动更改。\r\n" +
                    $"//     author MEIAM\r\n" +
                    $"// </auto-generated>\r\n" +
                    $"//------------------------------------------------------------------------------\r\n" +
                    $"\r\n" +
                    $"using System.ComponentModel.DataAnnotations;\r\n" +
                    $"{{using}}\r\n" +
                    $"\r\n" +
                    $"namespace { strSolutionName }.Model\r\n" +
                    $"{{\r\n" +
                    $"{{ClassDescription}}{{SugarTable}}{(blnSerializable ? "[Serializable]" : "")}\r\n" +
                    $"    public class {{ClassName}}{(string.IsNullOrEmpty(strInterface) ? "" : (" : " + strInterface))}\r\n" +
                    $"    {{\r\n" +
                    $"          public {{ClassName}}()\r\n" +
                    $"          {{\r\n" +
                    $"          }}\r\n" +
                    $"\r\n" +
                    $"{{PropertyName}}\r\n" +
                    $"    }}\r\n" +
                    $"}}";

                //字段样式
                var descriptionTemplate = $"" +
                    $"           /// <summary>\r\n" +
                    $"           /// 描述 : {{PropertyDescription}} \r\n" +
                    $"           /// 空值 : {{IsNullable}}\r\n" +
                    $"           /// 默认 : {{DefaultValue}}\r\n" +
                    $"           /// </summary>\r\n" +
                    $"           [Display(Name = \"{{PropertyDescription}}\")]";


                #endregion

                //启动生成
                var IDbFirst = Db.DbFirst;
                if (tableName != null && tableName.Length > 0)
                {
                    IDbFirst = IDbFirst.Where(tableName);
                }

                IDbFirst.IsCreateDefaultValue().IsCreateAttribute()
                    .SettingClassTemplate(p => p = classTemplate)
                    .SettingPropertyDescriptionTemplate(p => p = descriptionTemplate)
                     .CreateClassFile(strPath, $"{ strSolutionName }.Model");

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }

        }

        /// <summary>
        /// 生成DbContext.cs文件
        /// </summary>
        /// <param name="strPath">存放路径</param>
        /// <param name="strSolutionName">项目名称</param>
        public bool CreateDbContext(string strPath, string strSolutionName)
        {
            try
            {
                //获取数据库所有表
                var tables = Db.DbMaintenance.GetTableInfoList().Select(it => it.Name).ToList();

                #region 模板样式
                var classTemplate = $"" +
                    $"//------------------------------------------------------------------------------\r\n" +
                    $"// <auto-generated>\r\n" +
                    $"//     此代码已从模板生成手动更改此文件可能导致应用程序出现意外的行为。\r\n" +
                    $"//     如果重新生成代码，将覆盖对此文件的手动更改。\r\n" +
                    $"//     author MEIAM\r\n" +
                    $"// </auto-generated>\r\n" +
                    $"//------------------------------------------------------------------------------\r\n" +
                    $"\r\n" +
                    $"using { strSolutionName }.Common;\r\n" +
                    $"using { strSolutionName }.Model;\r\n" +
                    $"using System.Diagnostics;\r\n" +
                    $"using System.Linq;\r\n" +
                    $"using SqlSugar;\r\n" +
                    $"using System;\r\n" +
                    $"\r\n" +
                    $"namespace { strSolutionName }.Core\r\n" +
                    $"{{\r\n" +
                    $"        /// <summary>\r\n" +
                    $"        /// 数据库上下文\r\n" +
                    $"        /// </summary>\r\n" +
                    $"    public class DbContext\r\n" +
                    $"    {{\r\n" +
                    $"\r\n" +
                    $"        public SqlSugarClient Db;   //用来处理事务多表查询和复杂的操作\r\n" +
                    $"\r\n" +
                    $"        public static SqlSugarClient Current\r\n" +
                    $"        {{\r\n" +
                    $"            get\r\n" +
                    $"            {{\r\n" +
                    $"                return new SqlSugarClient(new ConnectionConfig()\r\n" +
                    $"                {{\r\n" +
                    $"                    ConnectionString = AppSettings.Configuration[\"DbConnection:ConnectionString\"],\r\n" +
                    $"                    DbType = (DbType)Convert.ToInt32(AppSettings.Configuration[\"DbConnection:DbType\"]),\r\n" +
                    $"                    IsAutoCloseConnection = false,\r\n" +
                    $"                    IsShardSameThread = true,\r\n" +
                    $"                    InitKeyType = InitKeyType.Attribute,\r\n" +
                    $"                    ConfigureExternalServices = new ConfigureExternalServices()\r\n" +
                    $"                    {{\r\n" +
                    $"                        DataInfoCacheService = new RedisCache()\r\n" +
                    $"                    }},\r\n" +
                    $"                    MoreSettings = new ConnMoreSettings()\r\n" +
                    $"                    {{\r\n" +
                    $"                        IsAutoRemoveDataCache = true\r\n" +
                    $"                    }}\r\n" +
                    $"                }});\r\n" +
                    $"            }}\r\n" +
                    $"        }}\r\n" +
                    $"\r\n" +
                    $"        public DbContext()\r\n" +
                    $"        {{\r\n" +
                    $"            Db = new SqlSugarClient(new ConnectionConfig()\r\n" +
                    $"            {{\r\n" +
                    $"                ConnectionString = AppSettings.Configuration[\"DbConnection:ConnectionString\"],\r\n" +
                    $"                DbType = (DbType)Convert.ToInt32(AppSettings.Configuration[\"DbConnection:DbType\"]),\r\n" +
                    $"                IsAutoCloseConnection = true,\r\n" +
                    $"                IsShardSameThread = true,\r\n" +
                    $"                InitKeyType = InitKeyType.Attribute,\r\n" +
                    $"                ConfigureExternalServices = new ConfigureExternalServices()\r\n" +
                    $"                {{\r\n" +
                    $"                    DataInfoCacheService = new RedisCache()\r\n" +
                    $"                }},\r\n" +
                    $"                MoreSettings = new ConnMoreSettings()\r\n" +
                    $"                {{\r\n" +
                    $"                    IsAutoRemoveDataCache = true\r\n" +
                    $"                }}\r\n" +
                    $"            }});\r\n" +
                    $"            //调式代码 用来打印SQL \r\n" +
                    $"            Db.Aop.OnLogExecuting = (sql, pars) =>\r\n" +
                    $"            {{\r\n" +
                    $"                Debug.WriteLine(sql);\r\n" +
                    $"            }};\r\n" +
                    $"        }}\r\n" +
                    $"\r\n" +
                    $"        public DbSet<T> DbTable<T>() where T : class, new()\r\n" +
                    $"        {{\r\n" +
                    $"            return new DbSet<T>(Db);\r\n" +
                    $"        }}\r\n" +
                    $"\r\n";

                foreach (var table in tables)
                {
                    classTemplate = classTemplate + $"        public DbSet<{table}> {table.Replace("_", "")}Db => new DbSet<{table}>(Db);\r\n";
                };

                classTemplate = classTemplate +
                    $"\r\n" +
                    $"    }}\r\n" +
                    $"\r\n" +
                    $"    /// <summary>\r\n" +
                    $"    /// 扩展ORM\r\n" +
                    $"    /// </summary>\r\n" +
                    $"    public class DbSet<T> : SimpleClient<T> where T : class, new()\r\n" +
                    $"    {{\r\n" +
                    $"        public DbSet(SqlSugarClient context) : base(context)\r\n" +
                    $"        {{\r\n" +
                    $"\r\n" +
                    $"        }}\r\n" +
                    $"    }}\r\n" +
                    $"\r\n" +
                    $"}}\r\n";
                #endregion

                File.WriteAllText(strPath, classTemplate);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 生成Service服务层
        /// </summary>
        /// <param name="strPath">存放路径</param>
        /// <param name="strSolutionName">项目名称</param>
        /// <param name="tableName">生产指定的表</param>
        /// <returns></returns>
        public bool CreateServices(string strPath, string strSolutionName, string tableName)
        {
            try
            {

                string saveFileName = $"{tableName.Replace("_", "")}Service.cs";

                #region 遍历子目录查找相同IService

                List<string> sourecFiles = new List<string>();

                GetFiles(strPath, sourecFiles);

                string readFilePath = sourecFiles.FirstOrDefault(m => m.Contains(saveFileName));

                string value = "";

                if (!string.IsNullOrEmpty(readFilePath))
                {
                    value = GetCustomValue(File.ReadAllText(readFilePath), "#region CustomInterface \r\n", "        #endregion\r\n");
                }

                #endregion

                #region 模板样式
                var classTemplate = $"" +
                    $"//------------------------------------------------------------------------------\r\n" +
                    $"// <auto-generated>\r\n" +
                    $"//     此代码已从模板生成手动更改此文件可能导致应用程序出现意外的行为。\r\n" +
                    $"//     如果重新生成代码，将覆盖对此文件的手动更改。\r\n" +
                    $"//     author MEIAM\r\n" +
                    $"// </auto-generated>\r\n" +
                    $"//------------------------------------------------------------------------------\r\n" +
                    $"using { strSolutionName }.Model;\r\n" +
                    $"using { strSolutionName }.Model.Dto;\r\n" +
                    $"using { strSolutionName }.Model.View;\r\n" +
                    $"using System.Collections.Generic;\r\n" +
                    $"using System.Threading.Tasks;\r\n" +
                    $"using SqlSugar;\r\n" +
                    $"using System.Linq;\r\n" +
                    $"using System;\r\n" +
                    $"\r\n" +
                    $"namespace { strSolutionName }.Interfaces\r\n" +
                    $"{{\r\n" +
                    $"    public class {tableName.Replace("_", "")}Service : BaseService<{tableName}>, I{tableName.Replace("_", "")}Service\r\n" +
                    $"    {{\r\n" +
                    $"\r\n" +
                    $"        public {tableName.Replace("_", "")}Service(IUnitOfWork unitOfWork) : base(unitOfWork)\r\n" +
                    $"        {{\r\n" +
                    $"        }}\r\n" +
                    $"\r\n" +
                    $"        #region CustomInterface \r\n" +
                    $"{(string.IsNullOrWhiteSpace(value) ? "" : value)}" +
                    $"        #endregion\r\n" +
                    $"\r\n" +
                    $"    }}\r\n" +
                    $"}}\r\n";
                #endregion

                File.WriteAllText($"{ strPath }\\{saveFileName}", classTemplate);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 生成IService服务层
        /// </summary>
        /// <param name="strPath">存放路径</param>
        /// <param name="strSolutionName">项目名称</param>
        /// <param name="tableName">生产指定的表</param>
        /// <returns></returns>
        public bool CreateIServices(string strPath, string strSolutionName, string tableName)
        {
            try
            {

                string saveFileName = $"I{tableName.Replace("_", "")}Service.cs";

                #region 遍历子目录查找相同IService

                List<string> sourecFiles = new List<string>();

                GetFiles(strPath, sourecFiles);

                string readFilePath = sourecFiles.FirstOrDefault(m => m.Contains(saveFileName));

                string value = "";

                if (!string.IsNullOrEmpty(readFilePath))
                {
                    value = GetCustomValue(File.ReadAllText(readFilePath), "#region CustomInterface \r\n", "        #endregion");
                }

                #endregion

                #region 模板样式
                var classTemplate = $"" +
                    $"//------------------------------------------------------------------------------\r\n" +
                    $"// <auto-generated>\r\n" +
                    $"//     此代码已从模板生成手动更改此文件可能导致应用程序出现意外的行为。\r\n" +
                    $"//     如果重新生成代码，将覆盖对此文件的手动更改。\r\n" +
                    $"//     author MEIAM\r\n" +
                    $"// </auto-generated>\r\n" +
                    $"//------------------------------------------------------------------------------\r\n" +
                    $"using { strSolutionName }.Model;\r\n" +
                    $"using { strSolutionName }.Model.Dto;\r\n" +
                    $"using { strSolutionName }.Model.View;\r\n" +
                    $"using System.Collections.Generic;\r\n" +
                    $"using System.Threading.Tasks;\r\n" +
                    $"using SqlSugar;\r\n" +
                    $"using System.Linq;\r\n" +
                    $"using System;\r\n" +
                    $"\r\n" +
                    $"namespace { strSolutionName }.Interfaces\r\n" +
                    $"{{\r\n" +
                    $"    public interface I{tableName.Replace("_", "")}Service : IBaseService<{tableName}>\r\n" +
                    $"    {{\r\n" +
                    $"\r\n" +
                    $"        #region CustomInterface \r\n" +
                    $"{(string.IsNullOrWhiteSpace(value) ? "" : value)}" +
                    $"        #endregion\r\n" +
                    $"\r\n" +
                    $"    }}\r\n" +
                    $"}}\r\n";
                #endregion

                File.WriteAllText($"{ strPath }\\{ saveFileName }", classTemplate);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 获取数据库所有表
        /// </summary>
        /// <returns></returns>
        public List<string> GetAllTables()
        {
            return Db.DbMaintenance.GetTableInfoList().Select(it => it.Name).ToList();
        }


        private string GetCustomValue(string customValue, string start, string end)
        {
            Regex r = new Regex("(?<=(" + start + "))[.\\s\\S]*?(?=(" + end + "))");
            return r.Match(customValue).Value;
        }

        private void GetFiles(string directory, List<string> list)
        {
            if (string.IsNullOrEmpty(directory)) return;

            DirectoryInfo d = new DirectoryInfo(directory);
            FileInfo[] files = d.GetFiles();
            DirectoryInfo[] directs = d.GetDirectories();

            foreach (FileInfo f in files)
            {
                list.Add(f.FullName);
            }

            //获取子文件夹内的文件列表，递归遍历  
            foreach (DirectoryInfo dd in directs)
            {
                GetFiles(dd.FullName, list);
            }
        }
        #endregion

    }
}
