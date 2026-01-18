using System.Data;
using System.Reflection;

namespace Academikus.AgenteInteligenteMentoresWebApi.Utility.Mapper
{
    /// <summary>
    /// Método de conversión de objetos
    /// </summary>
    public static class DataTableExtension
    {

        public static List<T> MapDataTableToList<T>(this DataTable dt)
        {
            try
            {
                if (dt == null | dt.Rows.Count < 0)
                {
                    return null;
                }

                List<T> list = new List<T>();

                foreach (DataRow dr in dt.Rows)
                {
                    list.Add(MapDataRowToObject<T>(dr));
                }

                return list;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }


        public static T MapDataRowToObject<T>(this DataRow dr)
        {
            T instance = Activator.CreateInstance<T>();
            try
            {

                if (dr == null)
                {
                    return instance;
                }

                PropertyInfo[] properties = instance.GetType().GetProperties();

                if ((properties.Length > 0))
                {
                    foreach (PropertyInfo propertyObject in properties)
                    {

                        bool valueSet = false;

                        foreach (object attributeObject in propertyObject.GetCustomAttributes(false))
                        {

                            if (object.ReferenceEquals(attributeObject.GetType(), typeof(MapperColumn)))
                            {
                                MapperColumn columnAttributeObject = (MapperColumn)attributeObject;

                                if ((columnAttributeObject.ColumnName != string.Empty))
                                {
                                    //If dr.Table.Columns.Contains(columnAttributeObject.ColumnName) AndAlso Not dr(columnAttributeObject.ColumnName) Is DBNull.Value Then
                                    if (dr.Table.Columns.Contains(columnAttributeObject.ColumnName) && !(dr[columnAttributeObject.ColumnName] is DBNull))
                                    {

                                        propertyObject.SetValue(instance, dr[columnAttributeObject.ColumnName], null);


                                        valueSet = true;

                                    }
                                }
                            }
                        }

                        if (!valueSet)
                        {
                            if (dr.Table.Columns.Contains(propertyObject.Name) && !(dr[propertyObject.Name] is DBNull))
                            {
                                propertyObject.SetValue(instance, dr[propertyObject.Name], null);
                            }

                        }
                    }
                }

                return instance;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }


        public static void ToCSV(this DataTable dtDataTable, string strFilePath)
        {
            StreamWriter sw = new StreamWriter(strFilePath, false);
            //headers    
            for (int i = 0; i < dtDataTable.Columns.Count; i++)
            {
                sw.Write(dtDataTable.Columns[i]);
                if (i < dtDataTable.Columns.Count - 1)
                {
                    sw.Write(",");
                }
            }
            sw.Write(sw.NewLine);
            foreach (DataRow dr in dtDataTable.Rows)
            {
                for (int i = 0; i < dtDataTable.Columns.Count; i++)
                {
                    if (!Convert.IsDBNull(dr[i]))
                    {
                        string value = dr[i].ToString();
                        if (value.Contains(","))
                        {
                            value = String.Format("\"{0}\"", value);
                            sw.Write(value);
                        }
                        else
                        {
                            sw.Write(dr[i].ToString());
                        }
                    }
                    if (i < dtDataTable.Columns.Count - 1)
                    {
                        sw.Write(",");
                    }
                }
                sw.Write(sw.NewLine);
            }
            sw.Close();
        }


    }





    [AttributeUsage(AttributeTargets.Property)]
    internal class MapperColumn : Attribute
    {

        private string mColumnName;

        public MapperColumn(string columnName)
        {
            mColumnName = columnName;
        }

        public string ColumnName
        {
            get { return mColumnName; }
            set { mColumnName = value; }
        }
    }
}
