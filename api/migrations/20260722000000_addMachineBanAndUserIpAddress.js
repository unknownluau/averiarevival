/**
 * Durable machine-ban state and the reserved user/IP association table.
 *
 * @param { import("knex").Knex } knex
 * @returns { Promise<void> }
 */
exports.up = async function (knex) {
  await knex.schema.alterTable('user_mac_address', (table) => {
    table.index(['mac_address', 'user_id'], 'user_mac_address_mac_user_idx');
  });

  await knex.schema.createTable('user_machine_ban', (table) => {
    table.bigInteger('user_id').unsigned().primary().references('id').inTable('user').onDelete('CASCADE');
    table.bigInteger('actor_user_id').unsigned().nullable().references('id').inTable('user').onDelete('SET NULL');
    table.string('internal_reason', 4096).nullable();
    table.timestamp('created_at', { useTz: true }).notNullable().defaultTo(knex.fn.now());
    table.timestamp('updated_at', { useTz: true }).notNullable().defaultTo(knex.fn.now());
    table.timestamp('revoked_at', { useTz: true }).nullable();

    table.index(['revoked_at', 'user_id'], 'user_machine_ban_active_idx');
  });

  // AccountStatus.MachineBanned is 7. The enum existed before the durable registry.
  await knex.raw(`
    INSERT INTO user_machine_ban (user_id, actor_user_id, internal_reason)
    SELECT source_user.id,
           CASE WHEN EXISTS (SELECT 1 FROM "user" AS fallback_user WHERE fallback_user.id = 1) THEN 1 ELSE NULL END,
           'Backfilled from AccountStatus.MachineBanned'
    FROM "user" AS source_user
    WHERE source_user.status = 7
    ON CONFLICT (user_id) DO NOTHING
  `);

  await knex.schema.createTable('machine_ban_enforcement', (table) => {
    table.bigInteger('user_id').unsigned().primary().references('id').inTable('user').onDelete('CASCADE');
    table.bigInteger('source_user_id').unsigned().notNullable().references('id').inTable('user').onDelete('CASCADE');
    table.bigInteger('actor_user_id').unsigned().nullable().references('id').inTable('user').onDelete('SET NULL');
    table.timestamp('execute_at', { useTz: true }).notNullable();
    table.timestamp('lease_until', { useTz: true }).nullable();
    table.integer('attempt_count').notNullable().defaultTo(0);
    table.string('last_error', 2048).nullable();
    table.timestamp('completed_at', { useTz: true }).nullable();
    table.timestamp('created_at', { useTz: true }).notNullable().defaultTo(knex.fn.now());
    table.timestamp('updated_at', { useTz: true }).notNullable().defaultTo(knex.fn.now());

    table.index(['completed_at', 'execute_at'], 'machine_ban_enforcement_due_idx');
  });

  await knex.schema.createTable('user_ip_address', (table) => {
    table.bigIncrements('id').primary();
    table.bigInteger('user_id').unsigned().notNullable().references('id').inTable('user').onDelete('CASCADE');
    table.string('ip_hash', 128).notNullable();
    table.string('action', 64).notNullable();
    table.timestamp('created_at', { useTz: true }).notNullable().defaultTo(knex.fn.now());
    table.timestamp('updated_at', { useTz: true }).notNullable().defaultTo(knex.fn.now());

    table.unique(['user_id', 'ip_hash', 'action'], { indexName: 'user_ip_address_user_hash_action_uq' });
    table.index(['ip_hash', 'action'], 'user_ip_address_hash_action_idx');
  });

  await knex.schema.createTable('user_ip_ban', (table) => {
    table.string('ip_hash', 128).primary();
    table.bigInteger('actor_user_id').unsigned().nullable().references('id').inTable('user').onDelete('SET NULL');
    table.string('internal_reason', 4096).notNullable();
    table.timestamp('created_at', { useTz: true }).notNullable().defaultTo(knex.fn.now());
    table.timestamp('updated_at', { useTz: true }).notNullable().defaultTo(knex.fn.now());
    table.timestamp('revoked_at', { useTz: true }).nullable();

    table.index(['revoked_at', 'ip_hash'], 'user_ip_ban_active_idx');
  });
};

/**
 * @param { import("knex").Knex } knex
 * @returns { Promise<void> }
 */
exports.down = async function (knex) {
  await knex.schema.dropTableIfExists('user_ip_ban');
  await knex.schema.dropTableIfExists('user_ip_address');
  await knex.schema.dropTableIfExists('machine_ban_enforcement');
  await knex.schema.dropTableIfExists('user_machine_ban');
  await knex.schema.alterTable('user_mac_address', (table) => {
    table.dropIndex(['mac_address', 'user_id'], 'user_mac_address_mac_user_idx');
  });
};
