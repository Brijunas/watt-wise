using System.CommandLine;

using WattWise.Cli.Commands;

RootCommand root = new("Watt-Wise maintenance CLI: migrations, one-off scripts and maintenance tasks.")
{
    MigrateCommand.Create(args),
};

return await root.Parse(args).InvokeAsync();
