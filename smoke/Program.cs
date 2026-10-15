using Turso.Data.Sqlite;

// Initialize Variables
using SqliteConnection connection = new("Data Source=:memory:");
int failures = 0;
// The column lists that select each index in fts_match and fts_score.
const string Words = "title, body";
const string Grams = "title_grams, body_grams";

// An in-memory database shaped like the Burvo search table. Turso takes one tokenizer per index, so word columns and
// their ngram copies get separate indexes; the app merges the two result sets.
connection.Open();
Execute(connection, """
	CREATE TABLE docs (id TEXT PRIMARY KEY, title TEXT NOT NULL, body TEXT NOT NULL, title_grams TEXT NOT NULL, body_grams TEXT NOT NULL);
	INSERT INTO docs VALUES ('tower', 'Tower Suites', 'Fifth floor corner suite near the lobby', 'Tower Suites', 'Fifth floor corner suite near the lobby');
	INSERT INTO docs VALUES ('acme', 'Acme Legal', 'Brief for a law firm needing twelve offices', 'Acme Legal', 'Brief for a law firm needing twelve offices');
	INSERT INTO docs VALUES ('towne', 'Towne Center', 'Retail space downtown', 'Towne Center', 'Retail space downtown');
	CREATE INDEX docs_words ON docs USING fts (title, body) WITH (weights = 'title=4.0,body=2.0');
	CREATE INDEX docs_grams ON docs USING fts (title_grams, body_grams) WITH (tokenizer = 'ngram', weights = 'title_grams=2.0,body_grams=1.0');
	""");

// Each check names the behavior the app relies on.
failures += Check(connection, "exact word", Words, "tower", "tower");
failures += Check(connection, "phrase", Words, "\"corner suite\"", "tower");
// Turso 0.8.1's query parser has no `word*` prefix syntax (it matches nothing), so prefixes go through the ngram index:
// a partial word is tokenized into grams that must appear in order, which is a substring match.
failures += Check(connection, "prefix through ngrams", Grams, "tow", "tower", "towne");
// A misspelled word's grams are not all in order in the text, so the app ORs its trigrams and filters the candidates
// by edit distance.
failures += Check(connection, "typo through trigrams", Grams, "tow OR owr", "tower");
failures += CheckHighlight(connection);
failures += CheckScoreOrder(connection);

Console.WriteLine(failures == 0 ? "PASS: full-text search works through Turso.Data.Sqlite." : $"FAIL: {failures} check(s) failed.");
return failures == 0 ? 0 : 1;

/// <summary>Runs one batch of statements with no results.</summary>
static void Execute(SqliteConnection connection, string sql)
{
	// Initialize Variables
	using SqliteCommand command = connection.CreateCommand();

	command.CommandText = sql;
	command.ExecuteNonQuery();
}

/// <summary>Checks that a query against one index's columns matches every expected id; returns 1 on failure.</summary>
static int Check(SqliteConnection connection, string name, string columns, string query, params string[] expected)
{
	// Initialize Variables
	using SqliteCommand command = connection.CreateCommand();
	List<string> ids = [];

	command.CommandText = $"SELECT id FROM docs WHERE fts_match({columns}, $q)";
	command.Parameters.AddWithValue("$q", query);
	using (SqliteDataReader reader = command.ExecuteReader())
	{
		while (reader.Read()) ids.Add(reader.GetString(0));
	}

	// A missing expected id is the failure; extra ngram matches are allowed and ranked below by the app.
	string[] missing = expected.Where(id => !ids.Contains(id)).ToArray();
	Console.WriteLine($"{(missing.Length == 0 ? "ok  " : "FAIL")} {name}: {query} -> {string.Join(", ", ids)}");
	return missing.Length == 0 ? 0 : 1;
}

/// <summary>Checks that fts_highlight wraps the matched word; returns 1 on failure.</summary>
static int CheckHighlight(SqliteConnection connection)
{
	// Initialize Variables
	using SqliteCommand command = connection.CreateCommand();
	string? highlighted;

	command.CommandText = "SELECT fts_highlight(title, '[', ']', 'tower') FROM docs WHERE id = 'tower'";
	highlighted = command.ExecuteScalar() as string;
	Console.WriteLine($"{(highlighted == "[Tower] Suites" ? "ok  " : "FAIL")} highlight -> {highlighted}");
	return highlighted == "[Tower] Suites" ? 0 : 1;
}

/// <summary>Checks that fts_score ranks a title match above a body-only match when sorted descending; returns 1 on failure.</summary>
static int CheckScoreOrder(SqliteConnection connection)
{
	// Initialize Variables
	using SqliteCommand command = connection.CreateCommand();
	List<string> ids = [];

	// A second row mentions "tower" only in its body, so the title-weighted row must outscore it.
	Execute(connection, "INSERT INTO docs VALUES ('note', 'Lease notes', 'Tower viewing booked', 'Lease notes', 'Tower viewing booked')");
	command.CommandText = "SELECT id, fts_score(title, body, 'tower') AS s FROM docs WHERE fts_match(title, body, 'tower') ORDER BY s DESC";
	using (SqliteDataReader reader = command.ExecuteReader())
	{
		while (reader.Read()) ids.Add(reader.GetString(0));
	}

	// Higher score means more relevant, so the title match comes first.
	Console.WriteLine($"{(ids.FirstOrDefault() == "tower" ? "ok  " : "FAIL")} score order (DESC) -> {string.Join(", ", ids)}");
	return ids.FirstOrDefault() == "tower" ? 0 : 1;
}
