-- Quiz Diritto internazionale - schema del database Supabase
-- Da eseguire una sola volta in Supabase: SQL Editor > New query > incolla > Run.

create table if not exists public.quiz_sessions (
    id               uuid primary key,
    user_id          uuid not null default auth.uid() references auth.users (id) on delete cascade,
    started_at       timestamptz not null,
    finished_at      timestamptz not null,
    total            int not null check (total > 0),
    correct          int not null check (correct >= 0 and correct <= total),
    duration_seconds int not null default 0
);

create table if not exists public.quiz_answers (
    id          bigint generated always as identity primary key,
    session_id  uuid not null references public.quiz_sessions (id) on delete cascade,
    user_id     uuid not null default auth.uid() references auth.users (id) on delete cascade,
    question_id text not null,
    topic       text not null,
    correct     boolean not null,
    answered_at timestamptz not null default now()
);

create index if not exists quiz_sessions_user_idx on public.quiz_sessions (user_id, started_at);
create index if not exists quiz_answers_user_idx on public.quiz_answers (user_id, answered_at);

-- Sicurezza: ogni utente vede e inserisce solo i propri dati (Row Level Security).
alter table public.quiz_sessions enable row level security;
alter table public.quiz_answers  enable row level security;

drop policy if exists "sessioni: lettura propria" on public.quiz_sessions;
drop policy if exists "sessioni: inserimento proprio" on public.quiz_sessions;
drop policy if exists "risposte: lettura propria" on public.quiz_answers;
drop policy if exists "risposte: inserimento proprio" on public.quiz_answers;

create policy "sessioni: lettura propria" on public.quiz_sessions
    for select to authenticated using (user_id = auth.uid());
create policy "sessioni: inserimento proprio" on public.quiz_sessions
    for insert to authenticated with check (user_id = auth.uid());

create policy "risposte: lettura propria" on public.quiz_answers
    for select to authenticated using (user_id = auth.uid());
create policy "risposte: inserimento proprio" on public.quiz_answers
    for insert to authenticated with check (
        user_id = auth.uid()
        and exists (select 1 from public.quiz_sessions s where s.id = session_id and s.user_id = auth.uid())
    );

grant select, insert on public.quiz_sessions to authenticated;
grant select, insert on public.quiz_answers  to authenticated;
