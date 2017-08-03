CREATE TABLE IF NOT EXISTS [movie] (
	[id] INTEGER PRIMARY KEY,
	[imdbId] TEXT,
	[title] TEXT NOT NULL,
	[year] INTEGER,
	[estimatedBudget] INTEGER,
	[boxOfficeGross] INTEGER,
	[stars] REAL,
	[rating] TEXT,
	[runningTime] INTEGER,
	[description] TEXT NOT NULL,
	[blurb] TEXT,
	[coverImage] TEXT
);

CREATE TABLE IF NOT EXISTS [cast] (
	[id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
	[fkMovieId] INTEGER NOT NULL,
	[name] TEXT NOT NULL,
	[role] TEXT NOT NULL,
	[img] TEXT NOT NULL,
	[gender] TEXT,
	[order] INTEGER,
	[isCrew] INTEGER DEFAULT 0,
	FOREIGN KEY ([fkMovieId]) REFERENCES movie([id]),
	UNIQUE([fkMovieId], [name], [role], [isCrew]) ON CONFLICT REPLACE
);

PRAGMA foreign_keys = ON;
