exports.up = async (knex) => {
  await knex.schema.createTable('donation_webhook_event', (table) => {
    table.bigIncrements('id').primary();
    table.string('provider', 32).notNullable();
    table.string('external_event_id', 128).notNullable();
    table.decimal('amount', 18, 2).notNullable();
    table.string('currency', 8).notNullable();
    table.string('donor_display_name', 128);
    table.bigInteger('user_id').unsigned();
    table.string('status', 32).notNullable();
    table.string('skip_reason', 128);
    table.timestamp('created_at').notNullable().defaultTo(knex.fn.now());
    table.timestamp('processed_at');
    table.unique(['provider', 'external_event_id']);
    table.index(['status', 'created_at']);
    table.index(['user_id', 'created_at']);
  });
};

exports.down = async (knex) => {
  await knex.schema.dropTable('donation_webhook_event');
};
