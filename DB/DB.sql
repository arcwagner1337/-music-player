-- WARNING: This schema is for context only and is not meant to be run.
-- Table order and constraints may not be valid for execution.

CREATE TABLE public.Favorite (
  id integer NOT NULL DEFAULT nextval('"Favorite_id_seq"'::regclass),
  name character varying NOT NULL,
  ID трека integer NOT NULL,
  author character varying NOT NULL,
  link text,
  track_time interval,
  CONSTRAINT Favorite_pkey PRIMARY KEY (id)
);
CREATE TABLE public.regular (
  id integer NOT NULL DEFAULT nextval('regular_id_seq'::regclass),
  ID трека integer NOT NULL,
  author character varying NOT NULL,
  link text,
  track_time interval,
  CONSTRAINT regular_pkey PRIMARY KEY (id)
);