-- 004_news_social.sql
-- Unified news/social ingestion + ticker linking.
-- Monthly partitions on published_at for scale + retention.

SET client_min_messages = WARNING;

-- Parent (partitioned) table
CREATE TABLE IF NOT EXISTS market.news_item (
  news_id          BIGINT GENERATED ALWAYS AS IDENTITY,
  kind             TEXT NOT NULL CHECK (kind IN ('NEWS','SOCIAL')), -- unified: headlines & posts
  source           TEXT NOT NULL,            -- e.g., 'REUTERS','BLOOMBERG','REDDIT','X','THREADS'
  external_id      TEXT,                     -- provider's id (tweet id, reddit id, article id)
  published_at     TIMESTAMPTZ NOT NULL,     -- event time (UTC) => PARTITION KEY
  url              TEXT,                     -- canonical url if available
  author           TEXT,
  lang             TEXT DEFAULT 'en',

  title            TEXT,
  content          TEXT,                     -- body or post text
  summary          TEXT,                     -- optional short summary

  -- Sentiment (optional; can be populated later by NLP pipeline)
  sentiment_label   TEXT CHECK (sentiment_label IN ('NEG','NEU','POS')),
  sentiment_score   NUMERIC(6,5),            -- e.g., VADER compound [-1,1]
  topic_tags        TEXT[],                  -- optional lightweight topics

  engagement        JSONB,                   -- e.g., {"likes":..., "retweets":..., "upvotes":...}
  raw               JSONB,                   -- original provider payload for provenance/debug

  -- Full-text search helper (generated)
  tsv_en tsvector GENERATED ALWAYS AS (
    to_tsvector('english',
      coalesce(title,'') || ' ' || coalesce(content,'')
    )
  ) STORED,

  -- IMPORTANT: PK on partitioned table must include the partition key
  PRIMARY KEY (published_at, news_id)
) PARTITION BY RANGE (published_at);

-- Helpful global (partitioned) indexes
CREATE INDEX IF NOT EXISTS ix_news_item_published_at
  ON market.news_item (published_at);

-- Trigram on title for fast ILIKE fuzzy search
CREATE INDEX IF NOT EXISTS ix_news_item_title_trgm
  ON market.news_item USING GIN (title gin_trgm_ops);

-- Full-text search (GIN on generated tsvector)
CREATE INDEX IF NOT EXISTS ix_news_item_tsv_en
  ON market.news_item USING GIN (tsv_en);

-- Optional JSONB index for raw/engagement lookups
CREATE INDEX IF NOT EXISTS ix_news_item_raw_gin
  ON market.news_item USING GIN (raw);

-- Non-unique helpers
CREATE INDEX IF NOT EXISTS ix_news_item_source_external
  ON market.news_item (source, external_id);

CREATE INDEX IF NOT EXISTS ix_news_item_url
  ON market.news_item (url);

COMMENT ON TABLE market.news_item IS 'Unified news & social items; monthly partitions on published_at.';

-- Link table: which instruments this item refers to (with optional relevance)
-- Reference the composite PK (published_at, news_id)
CREATE TABLE IF NOT EXISTS market.news_link (
  news_published_at TIMESTAMPTZ NOT NULL,
  news_id           BIGINT      NOT NULL,
  instrument_id     BIGINT      NOT NULL REFERENCES market.instrument(instrument_id) ON DELETE CASCADE,
  relevance         REAL,             -- 0..1 strength/confidence of linkage
  method            TEXT,             -- e.g., 'NER','RULES','HASHTAG','TICKER_SYMBOL'
  PRIMARY KEY (news_published_at, news_id, instrument_id),
  FOREIGN KEY (news_published_at, news_id)
    REFERENCES market.news_item(published_at, news_id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_news_link_instrument
  ON market.news_link (instrument_id);

-- Create monthly partitions for [now - 12 months, now + 3 months]
DO $$
DECLARE
  start_month DATE := date_trunc('month', current_date - INTERVAL '12 months')::date;
  end_month   DATE := date_trunc('month', current_date + INTERVAL '3 months')::date;
  d           DATE := start_month;
  next_d      DATE;
  part_name   TEXT;
BEGIN
  WHILE d <= end_month LOOP
    next_d := (d + INTERVAL '1 month')::date;
    part_name := format('news_item_%s', to_char(d, 'YYYY_MM'));

    EXECUTE format(
      'CREATE TABLE IF NOT EXISTS market.%I PARTITION OF market.news_item
       FOR VALUES FROM (%L) TO (%L);',
       part_name, d, next_d
    );

    d := next_d;
  END LOOP;
END $$;

