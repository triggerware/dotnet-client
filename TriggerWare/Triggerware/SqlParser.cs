using SqlParser;

namespace Triggerware;

public class SqlParser
{
    public void Main(string sql, RelDataElement[] source)
    {
        var ast = new SqlQueryParser().Parse(sql)[0].AsQuery()?.Body.AsSelect();
        if (ast == null) return;

        var tableNames = ast.From?.Select(x => x.Relation?.AsTable().Name).ToArray();
        var selection = ast.Selection;

        foreach (var name in tableNames)
            Console.WriteLine(name);

        foreach (var x in source)
            Console.WriteLine($"{x.Name}: {string.Join(",", x.SignatureNames)}, {string.Join(",", x.SignatureTypes)}");
    }
}