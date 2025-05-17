CREATE TABLE dbo.gits (
	git_id INT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
	created_at TIMESTAMPTZ DEFAULT (timezone('utc', now())),
	git_txt TEXT NOT NULL
);