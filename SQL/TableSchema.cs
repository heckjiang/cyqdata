using System;
using System.Collections.Generic;
using System.Text;
using CYQ.Data.Table;

using System.Data.Common;
using System.Data;
using System.IO;
using System.Data.OleDb;
using CYQ.Data.Cache;
using System.Reflection;
using CYQ.Data.Tool;


namespace CYQ.Data.SQL
{
    /// <summary>
    /// ���ṹ��
    /// </summary>
    internal partial class TableSchema
    {
        /// <summary>
        /// �������ݿ�Ĭ��ֵ��ʽ���ɱ�׼ֵ������׼ֵ��ԭ�ɸ����ݿ�Ĭ��ֵ
        /// </summary>
        /// <param name="flag">[0:ת�ɱ�׼ֵ],[1:ת�ɸ����ݿ�ֵ],[2:ת�ɸ����ݿ�ֵ�������ַ���ǰ��׺]</param>
        /// <param name="sqlDbType">���е�ֵ</param>
        /// <returns></returns>
        public static string FormatDefaultValue(DalType dalType, object value, int flag, SqlDbType sqlDbType)
        {
            string defaultValue = Convert.ToString(value).TrimEnd('\n');//oracle���Դ�\n��β
            if (dalType != DalType.Access)
            {
                defaultValue = defaultValue.Replace("GenGUID()", string.Empty);
            }
            if (defaultValue.Length == 0)
            {
                return null;
            }
            int groupID = DataType.GetGroup(sqlDbType);
            if (flag == 0)
            {
                if (groupID == 2)//���ڵı�׼ֵ
                {
                    return SqlValue.GetDate;
                }
                else if (groupID == 4)
                {
                    return SqlValue.GUID;
                }
                switch (dalType)
                {
                    case DalType.MySql://��ת\' \"�����Բ����滻��
                        defaultValue = defaultValue.Replace("\\\"", "\"").Replace("\\\'", "\'");
                        break;
                    case DalType.Access:
                    case DalType.SQLite:
                        defaultValue = defaultValue.Replace("\"\"", "��");
                        break;
                    default:
                        defaultValue = defaultValue.Replace("''", "��");
                        break;
                }
                switch (defaultValue.ToLower().Trim('(', ')'))
                {
                    case "newid":
                    case "guid":
                    case "sys_guid":
                    case "genguid":
                    case "uuid":
                        return SqlValue.GUID;
                }
            }
            else
            {
                if (defaultValue == SqlValue.GUID)
                {
                    switch (dalType)
                    {
                        case DalType.MsSql:
                        case DalType.Oracle:
                        case DalType.Sybase:
                            return SqlCompatible.FormatGUID(defaultValue, dalType);
                        default:
                            return "";
                    }

                }
            }
            switch (dalType)
            {
                case DalType.Access:
                    if (flag == 0)
                    {
                        if (defaultValue[0] == '"' && defaultValue[defaultValue.Length - 1] == '"')
                        {
                            defaultValue = defaultValue.Substring(1, defaultValue.Length - 2);
                        }
                    }
                    else
                    {
                        defaultValue = defaultValue.Replace(SqlValue.GetDate, "Now()").Replace("\"", "\"\"");
                        if (groupID == 0)
                        {
                            defaultValue = "\"" + defaultValue + "\"";
                        }
                    }
                    break;
                case DalType.MsSql:
                case DalType.Sybase:
                    if (flag == 0)
                    {
                        if (defaultValue.StartsWith("(") && defaultValue.EndsWith(")"))//���� (newid()) ��ȥ��()
                        {
                            defaultValue = defaultValue.Substring(1, defaultValue.Length - 2);
                        }
                        defaultValue = defaultValue.Trim('N', '\'');//'(', ')',
                    }
                    else
                    {
                        defaultValue = defaultValue.Replace(SqlValue.GetDate, "getdate()").Replace("'", "''");
                        if (groupID == 0)
                        {
                            defaultValue = "(N'" + defaultValue + "')";
                        }
                    }
                    break;
                case DalType.Oracle:
                    if (flag == 0)
                    {
                        defaultValue = defaultValue.Trim('\'');
                    }
                    else
                    {
                        defaultValue = defaultValue.Replace(SqlValue.GetDate, "sysdate").Replace("'", "''");
                        if (groupID == 0)
                        {
                            defaultValue = "'" + defaultValue + "'";
                        }
                    }
                    break;
                case DalType.MySql:
                    if (flag == 0)
                    {
                        defaultValue = defaultValue.Replace("b'0", "0").Replace("b'1", "1").Trim('\'');
                    }
                    else
                    {
                        defaultValue = defaultValue.Replace(SqlValue.GetDate, "CURRENT_TIMESTAMP").Replace("'", "\\'").Replace("\"", "\\\"");
                        if (groupID == 0)
                        {
                            defaultValue = "\"" + defaultValue + "\"";
                        }
                    }
                    break;
                case DalType.SQLite:
                    if (flag == 0)
                    {
                        defaultValue = defaultValue.Trim('"');
                        if (groupID > 0)//����һЩ���淶��д�����������͵ļ������� '0'
                        {
                            defaultValue = defaultValue.Trim('\'');
                        }
                    }
                    else
                    {
                        defaultValue = defaultValue.Replace(SqlValue.GetDate, "CURRENT_TIMESTAMP").Replace("\"", "\"\"");
                        if (groupID == 0)
                        {
                            defaultValue = "\"" + defaultValue + "\"";
                        }
                    }
                    break;
            }
            if (flag == 0)
            {
                return defaultValue.Replace("��", "\"").Replace("��", "'");
            }
            return defaultValue;
        }
        private static MDictionary<string, MDataColumn> columnCache = new MDictionary<string, MDataColumn>(StringComparer.OrdinalIgnoreCase);
        public static MDataColumn GetColumns(Type typeInfo)
        {
            if (columnCache.ContainsKey(typeInfo.FullName))
            {
                return columnCache[typeInfo.FullName];
            }
            else
            {
                #region ��ȡ�нṹ
                MDataColumn mdc = new MDataColumn();
                switch (StaticTool.GetSystemType(ref typeInfo))
                {
                    case SysType.Base:
                    case SysType.Enum:
                        mdc.Add(typeInfo.Name, DataType.GetSqlType(typeInfo), false);
                        return mdc;
                    case SysType.Generic:
                    case SysType.Collection:
                        Type[] argTypes;
                        Tool.StaticTool.GetArgumentLength(ref typeInfo, out argTypes);
                        foreach (Type type in argTypes)
                        {
                            mdc.Add(type.Name, DataType.GetSqlType(type), false);
                        }
                        argTypes = null;
                        return mdc;

                }

                PropertyInfo[] pis = StaticTool.GetPropertyInfo(typeInfo);

                SqlDbType sqlType;
                for (int i = 0; i < pis.Length; i++)
                {
                    sqlType = SQL.DataType.GetSqlType(pis[i].PropertyType);
                    mdc.Add(pis[i].Name, sqlType);
                    MCellStruct column = mdc[i];
                    column.MaxSize = DataType.GetMaxSize(sqlType);
                    if (i == 0)
                    {
                        column.IsPrimaryKey = true;
                        column.IsCanNull = false;

                        if (column.ColumnName.ToLower().Contains("id") && (column.SqlType == System.Data.SqlDbType.Int || column.SqlType == SqlDbType.BigInt))
                        {
                            column.IsAutoIncrement = true;
                        }
                    }
                    else if (i > pis.Length - 3 && sqlType == SqlDbType.DateTime && pis[i].Name.EndsWith("Time"))
                    {
                        column.DefaultValue = SqlValue.GetDate;
                    }
                }
                #endregion

                columnCache.Add(typeInfo.FullName, mdc);
                pis = null;
                return mdc;
            }

        }
        public static MDataColumn GetColumns(string tableName, ref DbBase dbHelper)
        {
            tableName = Convert.ToString(SqlCreate.SqlToViewSql(tableName));
            DalType dalType = dbHelper.dalType;
            tableName = SqlFormat.Keyword(tableName, dbHelper.dalType);
            switch (dalType)
            {
                case DalType.SQLite:
                case DalType.MySql:
                    tableName = SqlFormat.NotKeyword(tableName);
                    break;
                case DalType.Txt:
                case DalType.Xml:
                    tableName = Path.GetFileNameWithoutExtension(tableName);//��ͼ��������.���ģ��������
                    string fileName = dbHelper.Con.DataSource + tableName + (dalType == DalType.Txt ? ".txt" : ".xml");
                    return MDataColumn.CreateFrom(fileName);
            }

            MDataColumn mdcs = new MDataColumn();
            mdcs.dalType = dbHelper.dalType;
            //���table��helper����ͬһ����
            DbBase helper = dbHelper.ResetDbBase(tableName);
            helper.IsAllowRecordSql = false;//�ڲ�ϵͳ������¼SQL���ṹ��䡣
            try
            {
                bool isView = tableName.Contains(" ");//�Ƿ���ͼ��
                if (!isView)
                {
                    isView = Exists("V", tableName, ref helper);
                }
                MCellStruct mStruct = null;
                SqlDbType sqlType = SqlDbType.NVarChar;
                if (isView)
                {
                    string sqlText = SqlFormat.BuildSqlWithWhereOneEqualsTow(tableName);// string.Format("select * from {0} where 1=2", tableName);
                    mdcs = GetViewColumns(sqlText, ref helper);
                }
                else
                {
                    mdcs.AddRelateionTableName(SqlFormat.NotKeyword(tableName));
                    switch (dalType)
                    {
                        case DalType.MsSql:
                        case DalType.Oracle:
                        case DalType.MySql:
                        case DalType.Sybase:
                            #region Sql
                            string sql = string.Empty;
                            if (dalType == DalType.MsSql)
                            {
                                string dbName = null;
                                if (!helper.Version.StartsWith("08"))
                                {
                                    //�Ȼ�ȡͬ��ʣ�����Ƿ���
                                    string realTableName = Convert.ToString(helper.ExeScalar(string.Format(SynonymsName, SqlFormat.NotKeyword(tableName)), false));
                                    if (!string.IsNullOrEmpty(realTableName))
                                    {
                                        string[] items = realTableName.Split('.');
                                        tableName = realTableName;
                                        if (items.Length > 0)//�����
                                        {
                                            dbName = realTableName.Split('.')[0];
                                        }
                                    }
                                }

                                sql = GetMSSQLColumns(helper.Version.StartsWith("08"), dbName ?? helper.DataBase);
                            }
                            else if (dalType == DalType.MySql)
                            {
                                sql = GetMySqlColumns(helper.DataBase);
                            }
                            else if (dalType == DalType.Oracle)
                            {
                                sql = GetOracleColumns();
                            }
                            else if (dalType == DalType.Sybase)
                            {
                                tableName = SqlFormat.NotKeyword(tableName);
                                sql = GetSybaseColumns();
                            }
                            helper.AddParameters("TableName", tableName, DbType.String, 150, ParameterDirection.Input);
                            DbDataReader sdr = helper.ExeDataReader(sql, false);
                            if (sdr != null)
                            {
                                long maxLength;
                                bool isAutoIncrement = false;
                                short scale = 0;
                                string sqlTypeName = string.Empty;
                                while (sdr.Read())
                                {
                                    short.TryParse(Convert.ToString(sdr["Scale"]), out scale);
                                    if (!long.TryParse(Convert.ToString(sdr["MaxSize"]), out maxLength))//mysql�ĳ��ȿ��ܴ���int.MaxValue
                                    {
                                        maxLength = -1;
                                    }
                                    else if (maxLength > int.MaxValue)
                                    {
                                        maxLength = int.MaxValue;
                                    }
                                    sqlTypeName = Convert.ToString(sdr["SqlType"]);
                                    sqlType = DataType.GetSqlType(sqlTypeName);
                                    isAutoIncrement = Convert.ToBoolean(sdr["IsAutoIncrement"]);
                                    mStruct = new MCellStruct(mdcs.dalType);
                                    mStruct.ColumnName = Convert.ToString(sdr["ColumnName"]).Trim();
                                    mStruct.OldName = mStruct.ColumnName;
                                    mStruct.SqlType = sqlType;
                                    mStruct.IsAutoIncrement = isAutoIncrement;
                                    mStruct.IsCanNull = Convert.ToBoolean(sdr["IsNullable"]);
                                    mStruct.MaxSize = (int)maxLength;
                                    mStruct.Scale = scale;
                                    mStruct.Description = Convert.ToString(sdr["Description"]);
                                    mStruct.DefaultValue = FormatDefaultValue(dalType, sdr["DefaultValue"], 0, sqlType);
                                    mStruct.IsPrimaryKey = Convert.ToString(sdr["IsPrimaryKey"]) == "1";
                                    switch (dalType)
                                    {
                                        case DalType.MsSql:
                                        case DalType.MySql:
                                        case DalType.Oracle:
                                            mStruct.IsUniqueKey = Convert.ToString(sdr["IsUniqueKey"]) == "1";
                                            mStruct.IsForeignKey = Convert.ToString(sdr["IsForeignKey"]) == "1";
                                            mStruct.FKTableName = Convert.ToString(sdr["FKTableName"]);
                                            break;
                                    }

                                    mStruct.SqlTypeName = sqlTypeName;
                                    mStruct.TableName = SqlFormat.NotKeyword(tableName);
                                    mdcs.Add(mStruct);
                                }
                                sdr.Close();
                                if (dalType == DalType.Oracle && mdcs.Count > 0)//Ĭ��û���������ֻ�ܸ�������жϡ�
                                {
                                    MCellStruct firstColumn = mdcs[0];
                                    if (firstColumn.IsPrimaryKey && firstColumn.ColumnName.ToLower().Contains("id") && firstColumn.Scale == 0 && DataType.GetGroup(firstColumn.SqlType) == 1 && mdcs.JointPrimary.Count == 1)
                                    {
                                        firstColumn.IsAutoIncrement = true;
                                    }
                                }
                            }
                            #endregion
                            break;
                        case DalType.SQLite:
                            #region SQlite
                            if (helper.Con.State != ConnectionState.Open)
                            {
                                helper.Con.Open();
                            }
                            DataTable sqliteDt = helper.Con.GetSchema("Columns", new string[] { null, null, tableName });
                            if (!helper.isOpenTrans)
                            {
                                helper.Con.Close();
                            }
                            int size;
                            short sizeScale;
                            string dataTypeName = string.Empty;

                            foreach (DataRow row in sqliteDt.Rows)
                            {
                                object len = row["NUMERIC_PRECISION"];
                                if (len == null)
                                {
                                    len = row["CHARACTER_MAXIMUM_LENGTH"];
                                }
                                short.TryParse(Convert.ToString(row["NUMERIC_SCALE"]), out sizeScale);
                                if (!int.TryParse(Convert.ToString(len), out size))//mysql�ĳ��ȿ��ܴ���int.MaxValue
                                {
                                    size = -1;
                                }
                                dataTypeName = Convert.ToString(row["DATA_TYPE"]);
                                if (dataTypeName == "text" && size > 0)
                                {
                                    sqlType = DataType.GetSqlType("varchar");
                                }
                                else
                                {
                                    sqlType = DataType.GetSqlType(dataTypeName);
                                }
                                //COLUMN_NAME,DATA_TYPE,PRIMARY_KEY,IS_NULLABLE,CHARACTER_MAXIMUM_LENGTH AUTOINCREMENT

                                mStruct = new MCellStruct(row["COLUMN_NAME"].ToString(), sqlType, Convert.ToBoolean(row["AUTOINCREMENT"]), Convert.ToBoolean(row["IS_NULLABLE"]), size);
                                mStruct.Scale = sizeScale;
                                mStruct.Description = Convert.ToString(row["DESCRIPTION"]);
                                mStruct.DefaultValue = FormatDefaultValue(dalType, row["COLUMN_DEFAULT"], 0, sqlType);//"COLUMN_DEFAULT"
                                mStruct.IsPrimaryKey = Convert.ToBoolean(row["PRIMARY_KEY"]);
                                mStruct.SqlTypeName = dataTypeName;
                                mStruct.TableName = SqlFormat.NotKeyword(tableName);
                                mdcs.Add(mStruct);
                            }
                            #endregion
                            break;
                        case DalType.Access:
                            #region Access
                            DataTable keyDt, valueDt;
                            string sqlText = SqlFormat.BuildSqlWithWhereOneEqualsTow(tableName);// string.Format("select * from {0} where 1=2", tableName);
                            OleDbConnection con = new OleDbConnection(helper.Con.ConnectionString);
                            OleDbCommand com = new OleDbCommand(sqlText, con);
                            con.Open();
                            keyDt = com.ExecuteReader(CommandBehavior.KeyInfo).GetSchemaTable();
                            valueDt = con.GetOleDbSchemaTable(OleDbSchemaGuid.Columns, new object[] { null, null, SqlFormat.NotKeyword(tableName) });
                            con.Close();
                            con.Dispose();

                            if (keyDt != null && valueDt != null)
                            {
                                string columnName = string.Empty, sqlTypeName = string.Empty;
                                bool isKey = false, isCanNull = true, isAutoIncrement = false;
                                int maxSize = -1;
                                short maxSizeScale = 0;
                                SqlDbType sqlDbType;
                                foreach (DataRow row in keyDt.Rows)
                                {
                                    columnName = row["ColumnName"].ToString();
                                    isKey = Convert.ToBoolean(row["IsKey"]);//IsKey
                                    isCanNull = Convert.ToBoolean(row["AllowDBNull"]);//AllowDBNull
                                    isAutoIncrement = Convert.ToBoolean(row["IsAutoIncrement"]);
                                    sqlTypeName = Convert.ToString(row["DataType"]);
                                    sqlDbType = DataType.GetSqlType(sqlTypeName);
                                    short.TryParse(Convert.ToString(row["NumericScale"]), out maxSizeScale);
                                    if (Convert.ToInt32(row["NumericPrecision"]) > 0)//NumericPrecision
                                    {
                                        maxSize = Convert.ToInt32(row["NumericPrecision"]);
                                    }
                                    else
                                    {
                                        long len = Convert.ToInt64(row["ColumnSize"]);
                                        if (len > int.MaxValue)
                                        {
                                            maxSize = int.MaxValue;
                                        }
                                        else
                                        {
                                            maxSize = (int)len;
                                        }
                                    }
                                    mStruct = new MCellStruct(columnName, sqlDbType, isAutoIncrement, isCanNull, maxSize);
                                    mStruct.Scale = maxSizeScale;
                                    mStruct.IsPrimaryKey = isKey;
                                    mStruct.SqlTypeName = sqlTypeName;
                                    mStruct.TableName = SqlFormat.NotKeyword(tableName);
                                    foreach (DataRow item in valueDt.Rows)
                                    {
                                        if (columnName == item[3].ToString())//COLUMN_NAME
                                        {
                                            if (item[8].ToString() != "")
                                            {
                                                mStruct.DefaultValue = FormatDefaultValue(dalType, item[8], 0, sqlDbType);//"COLUMN_DEFAULT"
                                            }
                                            break;
                                        }
                                    }
                                    mdcs.Add(mStruct);
                                }

                            }

                            #endregion
                            break;
                    }
                }
                helper.ClearParameters();
            }
            catch (Exception err)
            {
                helper.debugInfo.Append(err.Message);
            }
            finally
            {
                helper.IsAllowRecordSql = true;//�ָ���¼SQL���ṹ��䡣
                if (helper != dbHelper)
                {
                    helper.Dispose();
                }
            }
            if (mdcs.Count > 0)
            {
                //�Ƴ�����־���У�
                string[] fields = AppConfig.DB.HiddenFields.Split(',');
                foreach (string item in fields)
                {
                    string key = item.Trim();
                    if (!string.IsNullOrEmpty(key) & mdcs.Contains(key))
                    {
                        mdcs.Remove(key);
                    }
                }
            }
            return mdcs;
        }
        internal static MDataColumn GetColumns(DataTable tableSchema)
        {
            MDataColumn mdcs = new MDataColumn();
            if (tableSchema != null && tableSchema.Rows.Count > 0)
            {
                mdcs.isViewOwner = true;
                string columnName = string.Empty, sqlTypeName = string.Empty, tableName = string.Empty;
                bool isKey = false, isCanNull = true, isAutoIncrement = false;
                int maxSize = -1;
                short maxSizeScale = 0;
                SqlDbType sqlDbType;
                string dataTypeName = "DataTypeName";
                if (!tableSchema.Columns.Contains(dataTypeName))
                {
                    dataTypeName = "DataType";
                }
                bool isHasAutoIncrement = tableSchema.Columns.Contains("IsAutoIncrement");
                bool isHasHidden = tableSchema.Columns.Contains("IsHidden");
                string hiddenFields = "," + AppConfig.DB.HiddenFields.ToLower() + ",";
                for (int i = 0; i < tableSchema.Rows.Count; i++)
                {
                    DataRow row = tableSchema.Rows[i];
                    tableName = Convert.ToString(row["BaseTableName"]);
                    mdcs.AddRelateionTableName(tableName);
                    if (isHasHidden && Convert.ToString(row["IsHidden"]) == "True")// !dcList.Contains(columnName))
                    {
                        continue;//�����Ǹ����������ֶΡ�
                    }
                    columnName = row["ColumnName"].ToString();
                    if (string.IsNullOrEmpty(columnName))
                    {
                        columnName = "Empty_" + i;
                    }
                    #region �����Ƿ�������
                    bool isHiddenField = hiddenFields.IndexOf("," + columnName + ",", StringComparison.OrdinalIgnoreCase) > -1;
                    if (isHiddenField)
                    {
                        continue;
                    }
                    #endregion

                    bool.TryParse(Convert.ToString(row["IsKey"]), out isKey);
                    bool.TryParse(Convert.ToString(row["AllowDBNull"]), out isCanNull);
                   // isKey = Convert.ToBoolean();//IsKey
                    //isCanNull = Convert.ToBoolean(row["AllowDBNull"]);//AllowDBNull
                    if (isHasAutoIncrement)
                    {
                        isAutoIncrement = Convert.ToBoolean(row["IsAutoIncrement"]);
                    }
                    sqlTypeName = Convert.ToString(row[dataTypeName]);
                    sqlDbType = DataType.GetSqlType(sqlTypeName);

                    if (short.TryParse(Convert.ToString(row["NumericScale"]), out maxSizeScale) && maxSizeScale == 255)
                    {
                        maxSizeScale = 0;
                    }
                    if (!int.TryParse(Convert.ToString(row["NumericPrecision"]), out maxSize) || maxSize == 255)//NumericPrecision
                    {
                        long len;
                        if (long.TryParse(Convert.ToString(row["ColumnSize"]), out len))
                        {
                            if (len > int.MaxValue)
                            {
                                maxSize = int.MaxValue;
                            }
                            else
                            {
                                maxSize = (int)len;
                            }
                        }
                    }
                    MCellStruct mStruct = new MCellStruct(columnName, sqlDbType, isAutoIncrement, isCanNull, maxSize);
                    mStruct.Scale = maxSizeScale;
                    mStruct.IsPrimaryKey = isKey;
                    mStruct.SqlTypeName = sqlTypeName;
                    mStruct.TableName = tableName;
                    mStruct.OldName = mStruct.ColumnName;
                    mdcs.Add(mStruct);

                }
                tableSchema = null;
            }
            return mdcs;
        }
        private static MDataColumn GetViewColumns(string sqlText, ref DbBase helper)
        {
            helper.OpenCon(null);
            helper.Com.CommandText = sqlText;
            DbDataReader sdr = helper.Com.ExecuteReader(CommandBehavior.KeyInfo);
            DataTable keyDt = null;
            if (sdr != null)
            {
                keyDt = sdr.GetSchemaTable();
                sdr.Close();
            }
            return GetColumns(keyDt);

        }
        public static Dictionary<string, string> GetTables(ref DbBase helper)
        {
            helper.IsAllowRecordSql = false;
            string sql = string.Empty;
            Dictionary<string, string> tables = null;
            switch (helper.dalType)
            {
                case DalType.MsSql:
                    sql = GetMSSQLTables(helper.Version.StartsWith("08"));
                    break;
                case DalType.Oracle:
                    sql = GetOracleTables();
                    break;
                case DalType.MySql:
                    sql = GetMySqlTables(helper.DataBase);
                    break;
                case DalType.Txt:
                case DalType.Xml:
                case DalType.Access:
                case DalType.SQLite:
                case DalType.Sybase:
                    string restrict = "TABLE";
                    if (helper.dalType == DalType.Sybase)
                    {
                        restrict = "BASE " + restrict;
                    }
                    tables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    helper.Con.Open();
                    DataTable dt = helper.Con.GetSchema("Tables", new string[] { null, null, null, restrict });
                    helper.Con.Close();
                    if (dt != null && dt.Rows.Count > 0)
                    {
                        string tableName = string.Empty;
                        foreach (DataRow row in dt.Rows)
                        {
                            tableName = Convert.ToString(row["TABLE_NAME"]);
                            if (!tables.ContainsKey(tableName))
                            {
                                tables.Add(tableName, string.Empty);
                            }
                            else
                            {
                                Log.WriteLogToTxt("Dictionary Has The Same TableName��" + tableName);
                            }
                        }
                        dt = null;
                    }
                    return tables;
            }
            if (tables == null)
            {
                tables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                DbDataReader sdr = helper.ExeDataReader(sql, false);
                if (sdr != null)
                {
                    string tableName = string.Empty;
                    while (sdr.Read())
                    {
                        tableName = Convert.ToString(sdr["TableName"]);
                        if (!tables.ContainsKey(tableName))
                        {
                            tables.Add(tableName, Convert.ToString(sdr["Description"]));
                        }
                        else
                        {
                            Log.WriteLogToTxt("Dictionary Has The Same TableName��" + tableName);
                        }
                    }
                    sdr.Close();
                    sdr = null;
                }
            }
            return tables;
        }



        #region ���ṹ����
        // private static CacheManage _SchemaCache = CacheManage.Instance;//Cache����
        internal static bool FillTableSchema(ref MDataRow row, ref DbBase dbBase, string tableName, string sourceTableName)
        {
            if (FillSchemaFromCache(ref row, ref dbBase, tableName, sourceTableName))
            {
                return true;
            }
            else//��Cache����ʧ��
            {
                return FillSchemaFromDb(ref row, ref dbBase, tableName, sourceTableName);
            }
        }

        /// <summary>
        /// ������ܹ�Key
        /// </summary>
        internal static string GetSchemaKey(string tableName, string dbName, DalType dalType)
        {
            string key = tableName;
            int start = key.IndexOf('(');
            int end = key.LastIndexOf(')');
            if (start > -1 && end > -1)//�Զ���table
            {
                key = "View" + Tool.MD5.Get(key);
            }
            else
            {
                if (key.IndexOf('.') > 0)
                {
                    dbName = key.Split('.')[0];
                }
                key = SqlFormat.NotKeyword(key);
            }
            return dalType + "_" + dbName + "_" + key;
        }
        private static bool FillSchemaFromCache(ref MDataRow row, ref DbBase dbBase, string tableName, string sourceTableName)
        {
            bool returnResult = false;

            string key = GetSchemaKey(tableName, dbBase.DataBase, dbBase.dalType);
            if (CacheManage.LocalInstance.Contains(key))//�������ȡ
            {
                try
                {
                    row = ((MDataColumn)CacheManage.LocalInstance.Get(key)).ToRow(sourceTableName);
                    returnResult = row.Count > 0;
                }
                catch (Exception err)
                {
                    Log.WriteLogToTxt(err);
                }
            }
            else if (!string.IsNullOrEmpty(AppConfig.DB.SchemaMapPath))
            {
                string fullPath = AppDomain.CurrentDomain.BaseDirectory + AppConfig.DB.SchemaMapPath + key + ".ts";
                if (System.IO.File.Exists(fullPath))
                {
                    MDataColumn mdcs = MDataColumn.CreateFrom(fullPath);
                    if (mdcs.Count > 0)
                    {
                        row = mdcs.ToRow(sourceTableName);
                        returnResult = row.Count > 0;
                        CacheManage.LocalInstance.Add(key, mdcs.Clone(), null, 1440);
                    }
                }
            }

            return returnResult;
        }
        private static bool FillSchemaFromDb(ref MDataRow row, ref DbBase dbBase, string tableName, string sourceTableName)
        {
            try
            {
                MDataColumn mdcs = null;
                //if (tableName.IndexOf('(') > -1 && tableName.IndexOf(')') > -1)//�Զ�����ͼtable
                //{
                //    dbBase.tempSql = "view";//ʹ��access��ʽ������
                //}
                mdcs = GetColumns(tableName, ref dbBase);
                if (mdcs.Count == 0)
                {
                    return false;
                }
                row = mdcs.ToRow(sourceTableName);
                row.TableName = sourceTableName;
                string key = GetSchemaKey(tableName, dbBase.DataBase, dbBase.dalType);
                CacheManage.LocalInstance.Add(key, mdcs.Clone(), null, 1440);

                switch (dbBase.dalType)//�ı����ݿⲻ���档
                {
                    case DalType.Access:
                    case DalType.SQLite:
                    case DalType.MsSql:
                    case DalType.MySql:
                    case DalType.Oracle:
                        if (!string.IsNullOrEmpty(AppConfig.DB.SchemaMapPath))
                        {
                            string folderPath = AppDomain.CurrentDomain.BaseDirectory + AppConfig.DB.SchemaMapPath;
                            if (System.IO.Directory.Exists(folderPath))
                            {
                                mdcs.WriteSchema(folderPath + key + ".ts");
                            }
                        }
                        break;
                }
                return true;

            }
            catch (Exception err)
            {
                Log.WriteLogToTxt(err);
                return false;
            }
        }
        #endregion

        /// <summary>
        /// �Ƿ���ڱ�����ͼ
        /// </summary>
        /// <param name="type">"U"��"V"</param>
        /// <param name="name">��������ͼ��</param>
        public static bool Exists(string type, string name, ref DbBase helper)
        {
            int result = 0;
            string exist = string.Empty;
            helper.IsAllowRecordSql = false;
            DalType dalType = helper.dalType;
            name = SqlFormat.Keyword(name, helper.dalType);
            switch (dalType)
            {
                case DalType.Access:
                    try
                    {
                        System.Data.OleDb.OleDbConnection con = new System.Data.OleDb.OleDbConnection(helper.Con.ConnectionString);
                        con.Open();
                        result = con.GetOleDbSchemaTable(System.Data.OleDb.OleDbSchemaGuid.Tables, new object[] { null, null, SqlFormat.NotKeyword(name), "Table" }).Rows.Count;
                        con.Close();
                    }
                    catch (Exception err)
                    {
                        Log.WriteLogToTxt(err);
                    }
                    break;
                case DalType.MySql:
                    if (type != "V" || (type == "V" && name.ToLower().StartsWith("v_")))//��ͼ����v_��ͷ
                    {
                        exist = string.Format(ExistMySql, SqlFormat.NotKeyword(name), helper.DataBase);
                    }
                    break;
                case DalType.Oracle:
                    exist = string.Format(ExistOracle, (type == "U" ? "TABLE" : "VIEW"), name);
                    break;
                case DalType.MsSql:
                    exist = string.Format(helper.Version.StartsWith("08") ? Exist2000 : Exist2005, name, type);
                    break;
                case DalType.SQLite:
                    exist = string.Format(ExistSqlite, (type == "U" ? "table" : "view"), SqlFormat.NotKeyword(name));
                    break;
                case DalType.Sybase:
                    exist = string.Format(ExistSybase, SqlFormat.NotKeyword(name), type);
                    break;
                case DalType.Txt:
                case DalType.Xml:
                    string folder = helper.Con.DataSource + Path.GetFileNameWithoutExtension(name);
                    FileInfo info = new FileInfo(folder + ".ts");
                    result = (info.Exists && info.Length > 10) ? 1 : 0;
                    if (result == 0)
                    {
                        info = new FileInfo(folder + (dalType == DalType.Txt ? ".txt" : ".xml"));
                        result = (info.Exists && info.Length > 10) ? 1 : 0;
                    }
                    break;
            }
            if (exist != string.Empty)
            {
                helper.IsAllowRecordSql = false;
                result = Convert.ToInt32(helper.ExeScalar(exist, false));
            }
            return result > 0;
        }

        #region ICloneable ��Ա


        #endregion
    }
    internal partial class TableSchema
    {
        internal const string Exist2000 = "SELECT count(*) FROM sysobjects where id = OBJECT_ID(N'{0}') AND xtype in (N'{1}')";
        internal const string Exist2005 = "SELECT count(*) FROM sys.objects where object_id = OBJECT_ID(N'{0}') AND type in (N'{1}')";
        internal const string ExistOracle = "Select count(*)  From user_objects where object_type='{0}' and object_name=upper('{1}')";
        internal const string ExistMySql = "SELECT count(*)  FROM  `information_schema`.`COLUMNS`  where TABLE_NAME='{0}' and TABLE_SCHEMA='{1}'";
        internal const string ExistSybase = "SELECT count(*) FROM sysobjects where id = OBJECT_ID(N'{0}') AND type in (N'{1}')";
        internal const string ExistSqlite = "SELECT count(*) FROM sqlite_master where type='{0}' and name='{1}'";
        internal const string ExistOracleSequence = "SELECT count(*) FROM All_Sequences where Sequence_name='{0}'";
        internal const string CreateOracleSequence = "create sequence {0} start with {1} increment by 1";
        internal const string GetOracleMaxID = "select max({0}) from {1}";

        #region ��ȡ���ݿ�������ֶ�
        private const string SynonymsName = "SELECT TOP 1 base_object_name from sys.synonyms WHERE NAME = '{0}'";
        internal static string GetMSSQLColumns(bool for2000, string dbName)
        {
            // 2005��������ͬ���֧�֡� case s2.name WHEN 'timestamp' THEN 'variant' ELSE s2.name END as [SqlType],
            return string.Format(@"select s1.name as ColumnName,case s2.name WHEN 'uniqueidentifier' THEN 36 
                     WHEN 'ntext' THEN -1 WHEN 'text' THEN -1 WHEN 'image' THEN -1 else s1.[prec] end  as [MaxSize],s1.scale as [Scale],
                     isnullable as [IsNullable],colstat&1 as [IsAutoIncrement],s2.name as [SqlType],
                     case when exists(SELECT 1 FROM {0}..sysobjects where xtype='PK' and name in (SELECT name FROM {0}..sysindexes WHERE id=s1.id and 
                     indid in(SELECT indid FROM {0}..sysindexkeys WHERE id=s1.id AND colid=s1.colid))) then 1 else 0 end as [IsPrimaryKey],
                     case when exists(SELECT 1 FROM {0}..sysobjects where xtype='UQ' and name in (SELECT name FROM {0}..sysindexes WHERE id=s1.id and 
                     indid in(SELECT indid FROM {0}..sysindexkeys WHERE id=s1.id AND colid=s1.colid))) then 1 else 0 end as [IsUniqueKey],
                     case when s5.constid>0 then 1 else 0 end as [IsForeignKey],
                     case when s5.rkeyid>0 then object_name(s5.rkeyid) else null end [FKTableName],
                     isnull(s3.text,'') as [DefaultValue],
                     s4.value as Description
                     from {0}..syscolumns s1 right join {0}..systypes s2 on s2.xtype =s1.xtype  
                     left join {0}..syscomments s3 on s1.cdefault=s3.id  " +
                     (for2000 ? "left join {0}..sysproperties s4 on s4.id=s1.id and s4.smallid=s1.colid  " : "left join {0}.sys.extended_properties s4 on s4.major_id=s1.id and s4.minor_id=s1.colid")
                     + " left join {0}..sysforeignkeys s5 on s5.fkeyid=s1.id and s5.fkey=s1.colid where s1.id=object_id(@TableName) and s2.name<>'sysname' and s2.usertype<100 order by s1.colid", "[" + dbName + "]");
        }
        internal static string GetOracleColumns()
        {
            return @"select A.COLUMN_NAME as ColumnName,case DATA_TYPE when 'DATE' then 23 when 'CLOB' then 2147483647 when 'NCLOB' then 1073741823 else case when CHAR_COL_DECL_LENGTH is not null then CHAR_COL_DECL_LENGTH
                    else   case when DATA_PRECISION is not null then DATA_PRECISION else DATA_LENGTH end   end end as MaxSize,DATA_SCALE as Scale,
                    case NULLABLE when 'Y' then 1 else 0 end as IsNullable,
                    0 as IsAutoIncrement,
                    case  when (DATA_TYPE='NUMBER' and DATA_SCALE>0 and DATA_PRECISION<13)  then 'float'
                      when (DATA_TYPE='NUMBER' and DATA_SCALE>0 and DATA_PRECISION<22)  then 'double'
                        when (DATA_TYPE='NUMBER' and DATA_SCALE=0 and DATA_PRECISION<11)  then 'int'
                          when (DATA_TYPE='NUMBER' and DATA_SCALE=0 and DATA_PRECISION<20)  then 'long'
                                when DATA_TYPE='NUMBER' then'decimal'                   
                    else DATA_TYPE end as SqlType,
                    case when v.CONSTRAINT_TYPE='P' then 1 else 0 end as IsPrimaryKey,
                      case when v.CONSTRAINT_TYPE='U' then 1 else 0 end as IsUniqueKey,
                        case when v.CONSTRAINT_TYPE='R' then 1 else 0 end as IsForeignKey,
                         v.FKTableName,
                    data_default as DefaultValue,
                    COMMENTS AS Description
                    from USER_TAB_COLS A left join user_col_comments B on A.Table_Name = B.Table_Name and A.Column_Name = B.Column_Name 
                    left join
                    (select uc1.table_name,ucc.column_name, uc1.constraint_type,uc2.table_name as FKTableName from user_constraints uc1
                    left join  user_constraints uc2 on uc1.r_constraint_name=uc2.constraint_name
                    left join user_cons_columns ucc on ucc.constraint_name=uc1.constraint_name
                    where uc1.constraint_type in('P','U','R') ) v
                    on A.TABLE_NAME=v.table_name and A.COLUMN_NAME=v.column_name

                    where A.TABLE_NAME= nvl((SELECT TABLE_NAME FROM USER_SYNONYMS WHERE SYNONYM_NAME=UPPER(:TableName) and rownum=1),UPPER(:TableName)) order by COLUMN_ID";
        }
        internal static string GetMySqlColumns(string dbName)
        {
            return string.Format(@"SELECT DISTINCT s1.COLUMN_NAME as ColumnName,case DATA_TYPE when 'int' then 10 when 'date' then 10 when 'time' then 8  when 'datetime' then 23 when 'year' then 4
                    else IFNULL(CHARACTER_MAXIMUM_LENGTH,NUMERIC_PRECISION) end as MaxSize,NUMERIC_SCALE as Scale,
                    case IS_NULLABLE when 'YES' then 1 else 0 end as IsNullable,
                    CASE extra when 'auto_increment' then 1 else 0 END AS IsAutoIncrement,
                    DATA_TYPE as SqlType,
                    case Column_key WHEN 'PRI' then 1 else 0 end as IsPrimaryKey,
                    case s3.CONSTRAINT_TYPE when 'UNIQUE' then 1 else 0 end as IsUniqueKey,
					case s3.CONSTRAINT_TYPE when 'FOREIGN KEY' then 1 else 0 end as IsForeignKey,
					s2.REFERENCED_TABLE_NAME as FKTableName,
                    COLUMN_DEFAULT AS DefaultValue,
                    COLUMN_COMMENT AS Description
                    FROM  `information_schema`.`COLUMNS` s1
					LEFT JOIN `information_schema`.KEY_COLUMN_USAGE s2 on s2.TABLE_SCHEMA=s1.TABLE_SCHEMA and s2.TABLE_NAME=s1.TABLE_NAME and s2.COLUMN_NAME=s1.COLUMN_NAME
					LEFT JOIN `information_schema`.TABLE_CONSTRAINTS s3 on s3.TABLE_SCHEMA=s2.TABLE_SCHEMA and s3.TABLE_NAME=s2.TABLE_NAME and s3.CONSTRAINT_NAME=s2.CONSTRAINT_NAME
                    where s1.TABLE_SCHEMA='{0}' and  s1.TABLE_NAME=?TableName order by s1.ORDINAL_POSITION", dbName);
        }
        internal static string GetSybaseColumns()
        {
            return @"select 
s1.name as ColumnName,
s1.length as MaxSize,
s1.scale as Scale,
case s1.status&8 when 8 then 1 ELSE 0 END AS IsNullable,
case s1.status&128 when 128 then 1 ELSE 0 END as IsAutoIncrement,
s2.name as SqlType,
case when exists(SELECT 1 FROM sysindexes WHERE id=s1.id AND s1.name=index_col(@TableName,indid,s1.colid)) then 1 else 0 end as IsPrimaryKey,
               str_replace(s3.text,'DEFAULT  ',null) as DefaultValue,
               null as Description
from syscolumns s1 left join systypes s2 on s1.usertype=s2.usertype
left join syscomments s3 on s1.cdefault=s3.id
where s1.id =object_id(@TableName) and s2.usertype<100
order by s1.colid";
        }

        internal static string GetMSSQLTables(bool for2000)
        {
            return @"Select o.name as TableName, p.value as Description from sysobjects o " + (for2000 ? "left join sysproperties p on p.id = o.id and smallid = 0" : "left join sys.extended_properties p on p.major_id = o.id and minor_id = 0")
                + " and p.name = 'MS_Description' where o.type = 'U' AND o.name<>'dtproperties' AND o.name<>'sysdiagrams'" + (for2000 ? "" : " and category=0");
        }
        internal static string GetOracleTables()
        {
            return "select TABLE_NAME AS TableName,COMMENTS AS Description from user_tab_comments";
        }
        internal static string GetMySqlTables(string dbName)
        {
            return string.Format("select TABLE_NAME as TableName,TABLE_COMMENT as Description from `information_schema`.`TABLES`  where TABLE_SCHEMA='{0}'", dbName);
        }
        #endregion
    }
}