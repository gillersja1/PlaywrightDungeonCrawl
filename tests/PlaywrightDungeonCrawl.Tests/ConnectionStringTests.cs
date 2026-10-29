using Npgsql;
using PlaywrightDungeonCrawl.Dungeon;

namespace PlaywrightDungeonCrawl.Tests;

public class ConnectionStringTests
{
    [Test]
    public void NeonUrlsBecomeNpgsqlConnectionStrings()
    {
        var normalized = new NpgsqlConnectionStringBuilder(PostgresDatabase.NormalizeConnectionString(
            "postgresql://crawler:p%40ss@ep-cool-name-123.eu-central-1.aws.neon.tech/dungeon?sslmode=require&channel_binding=require"));

        Assert.Multiple(() =>
        {
            Assert.That(normalized.Host, Is.EqualTo("ep-cool-name-123.eu-central-1.aws.neon.tech"));
            Assert.That(normalized.Port, Is.EqualTo(5432));
            Assert.That(normalized.Database, Is.EqualTo("dungeon"));
            Assert.That(normalized.Username, Is.EqualTo("crawler"));
            Assert.That(normalized.Password, Is.EqualTo("p@ss"));
            Assert.That(normalized.SslMode, Is.EqualTo(SslMode.Require));
        });
    }

    [Test]
    public void NpgsqlConnectionStringsPassThrough()
    {
        const string connectionString = "Host=localhost;Database=dungeon;Username=postgres;Password=postgres";

        Assert.That(PostgresDatabase.NormalizeConnectionString(connectionString), Is.EqualTo(connectionString));
    }
}
