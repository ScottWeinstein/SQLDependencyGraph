using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Linq;
using QuickGraph;
using QuickGraph.Serialization;
using QuickGraph.Serialization.DirectedGraphML;
using ScottW;

namespace SQLDependencyGraph
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var excludesObjTyps = new string[] { "INTERNAL_TABLE", "SYSTEM_TABLE" };
            var excludeDBs = new string[] { "master", "tempdb", "model", "msdb" };
            DbInfo dc;
            IEnumerable<string> dbs;

            string server = ".";

            string connStr = String.Format("Data Source={0};Integrated Security=True", server);
            using (dc = new DbInfo(connStr))
            {
                dbs = dc.databases.Where(db => !excludeDBs.Contains(db.name)).Select(db => db.name).ToArray();
            }

            var allDbObjTypes = dbs.SelectMany(dataBase =>
                 {
                     using (dc = new DbInfo(string.Format("Data Source=.;Initial Catalog={0};Integrated Security=True", dataBase)))
                     {
                         var objsForDB = from dbobjs in dc.all_objects
                                         join schma in dc.schemas on dbobjs.schema_id equals schma.schema_id
                                         where !excludesObjTyps.Contains(dbobjs.type_desc)
                                         && !dbobjs.type_desc.Contains("CONSTRAINT")
                                         && !dbobjs.is_ms_shipped
                                         select new { Name = String.Format("{0}.{1}.{2}", dataBase, schma.name, dbobjs.name), Type = dbobjs.type_desc };
                         return objsForDB.ToArray();
                     }
                 }).ToDictionary(item => item.Name, item => item.Type);

            var allDependencies = dbs.SelectMany(dataBase =>
                 {
                     string connStr2 = String.Format("Data Source={0};Initial Catalog={1};Integrated Security=True", server, dataBase);
                     using (dc = new DbInfo(connStr2))
                     {
                         var objsForDB = from dbobjs in dc.all_objects
                                         join schma in dc.schemas on dbobjs.schema_id equals schma.schema_id
                                         where !excludesObjTyps.Contains(dbobjs.type_desc)
                                         && !dbobjs.type_desc.Contains("CONSTRAINT")
                                         && !dbobjs.is_ms_shipped
                                         select new
                                         {
                                             NameForLookup = String.Format("{1}.{2}", dataBase, schma.name, dbobjs.name),
                                             NameForGraph = String.Format("{0}.{1}.{2}", dataBase, schma.name, dbobjs.name)
                                         };

                         var deps = objsForDB.ToArray().Select(item => StatementExpressions.TryCatch(() => dc.dm_sql_referenced_entities(item.NameForLookup, "OBJECT")
                                                        .Where(dep => dep.referenced_class == 1 && dep.referenced_minor_id == 0)
                                                        .Select(dep => new Tuple<string, string, string>(item.NameForGraph,
                                string.Format("{0}.{1}.{2}", dep.referenced_database_name ?? dataBase, dep.referenced_schema_name ?? "dbo", dep.referenced_entity_name),
                                                                                                    dep.referenced_class_desc)).ToArray(),
                                                                                           (SqlException x) => Debug.Write(x), 
                                                                                           () => null));
                         var hot = deps.ToArray();
                         return hot.Where(item => item != null).SelectMany(l => l);
                     }
                 }).ToArray();

            DirectedGraph graphToDirectedGraphML = BuildGraph(allDbObjTypes, allDependencies);
            graphToDirectedGraphML.WriteXml("SqlDeps.dgml");
            Console.WriteLine("Open file 'SqlDeps.dgml'");
        }

        private static DirectedGraph BuildGraph(Dictionary<string, string> allDbObjTypes, Tuple<string, string, string>[] allDependencies)
        {
            var graph = new AdjacencyGraph<SqlItem, Edge<SqlItem>>();
            foreach (var item in allDependencies)
            {
                string objType1, objType2;
                objType1 = allDbObjTypes.TryGetValue(item.Item1, out objType1) ? objType1 : "Unknown";
                objType2 = allDbObjTypes.TryGetValue(item.Item2, out objType2) ? objType2 : "Unknown";
                SqlItem sqlItem1 = new SqlItem(item.Item1, objType1);
                SqlItem sqlItem2 = new SqlItem(item.Item2, objType2);
                Edge<SqlItem> newEdge = new Edge<SqlItem>(sqlItem1, sqlItem2);
                graph.AddVerticesAndEdge(newEdge);
            }

            foreach (string item in allDbObjTypes.Values.Distinct())
            {
                Console.WriteLine(item);
            }

            var graphToDirectedGraphML = graph.ToDirectedGraphML(vertex => vertex.Name,
                                                                ege => string.Empty,
                                                                (sqI, dgn) => { dgn.TypeName = GetShortSqlTypeName(sqI.SqlType); },
                                                                (eqi, dgl) => { });
            return graphToDirectedGraphML;
        }

        public static string GetShortSqlTypeName(string sqlTypeName)
        {
            switch (sqlTypeName)
            {
                case "USER_TABLE":
                    return "table";
                case "SQL_STORED_PROCEDURE":
                    return "SP";
                case "SQL_SCALAR_FUNCTION":
                    return "udf";
                case "SQL_TRIGGER":
                    return "trigger";
                case "VIEW":
                    return "view";
                case "SQL_TABLE_VALUED_FUNCTION":
                case "SQL_INLINE_TABLE_VALUED_FUNCTION":
                return "table udf";
                default:
                        return sqlTypeName;
            }
        }
    }
}
