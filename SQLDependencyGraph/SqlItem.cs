using System;

namespace SQLDependencyGraph
{
    public class SqlItem : IEquatable<SqlItem>
    {
        public SqlItem(string name, string sqlType)
        {
            Name = name;
            SqlType = sqlType;
        }

        public string Name { get; set; }
        public string SqlType { get; set; }

        public bool Equals(SqlItem other)
        {
            return other != null && this.Name == other.Name && this.SqlType == other.SqlType;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as SqlItem);
        }

        public override int GetHashCode()
        {
            return (Name + SqlType).GetHashCode();
        }
    }
}
