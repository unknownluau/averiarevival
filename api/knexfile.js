function getConfig() {
  // for docker support
  if (process.env.DATABASE_URL) {
    return { client: 'pg', connection: process.env.DATABASE_URL };
  }

  // try individual env vars (fall back))
  if (process.env.DB_HOST) {
    return {
      client: 'pg',
      connection: {
        host:     process.env.DB_HOST,
        port:     Number(process.env.DB_PORT) || 5432,
        user:     process.env.DB_USER,
        password: process.env.DB_PASS,
        database: process.env.DB_NAME,
      },
    };
  }

  // last fallback read config.json (local dev without docker)
  try {
    return JSON.parse(require('fs').readFileSync('./config.json').toString()).knex;
  } catch {
    throw new Error(
      'No DB config found. Set DATABASE_URL, DB_HOST/DB_USER/DB_PASS/DB_NAME, or create ./api/config.json.'
    );
  }
}

module.exports = getConfig();