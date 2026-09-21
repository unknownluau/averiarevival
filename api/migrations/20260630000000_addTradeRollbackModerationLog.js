exports.up = async (knex) => {
  await knex.schema.createTable('moderation_rollback_trade', (table) => {
    table.bigIncrements('id').primary();
    table.bigInteger('trade_id').notNullable().unsigned();
    table.bigInteger('actor_id').notNullable().unsigned();
    table.bigInteger('user_id_one').notNullable().unsigned();
    table.bigInteger('user_id_two').notNullable().unsigned();
    table.timestamp('created_at').notNullable().defaultTo(knex.fn.now());

    table.index(['trade_id']);
    table.index(['actor_id', 'created_at']);
  });
};

exports.down = async (knex) => {
  await knex.schema.dropTable('moderation_rollback_trade');
};
