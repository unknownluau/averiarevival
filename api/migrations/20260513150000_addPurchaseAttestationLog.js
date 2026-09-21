exports.up = async (knex) => {
    await knex.schema.createTable('purchase_attestation_log', (t) => {
        t.bigIncrements('id').primary();
        t.bigInteger('user_id').notNullable().unsigned();
        t.string('ticket_id', 32).notNullable();
        t.bigInteger('asset_id').unsigned();
        t.bigInteger('expected_price');
        t.smallint('outcome').notNullable();
        t.string('client_ip_hash', 128);
        t.string('user_agent', 512);
        t.timestamp('issued_at').notNullable().defaultTo(knex.fn.now());
        t.timestamp('consumed_at');
        t.unique(['user_id', 'ticket_id']);
        t.index(['user_id', 'issued_at']);
        t.index(['outcome', 'issued_at']);
    });
};

exports.down = async (knex) => {
    await knex.schema.dropTable('purchase_attestation_log');
};
