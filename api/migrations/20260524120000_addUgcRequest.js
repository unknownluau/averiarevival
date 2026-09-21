exports.up = async (knex) => {
    await knex.schema.createTable('ugc_request', (t) => {
        t.bigIncrements('id').primary();
        t.bigInteger('user_id').notNullable().unsigned();
        t.bigInteger('roblox_asset_id').notNullable().unsigned();
        t.string('roblox_url', 512).notNullable();
        t.string('item_name', 256);
        t.smallint('status').notNullable().defaultTo(0); // 0=Pending,1=Approved,2=Declined
        t.bigInteger('decided_by').unsigned();
        t.bigInteger('created_asset_id').unsigned();
        t.timestamp('created_at').notNullable().defaultTo(knex.fn.now());
        t.timestamp('decided_at');
        t.index(['user_id', 'created_at']);
        t.index(['status', 'created_at']);
    });
};

exports.down = async (knex) => {
    await knex.schema.dropTable('ugc_request');
};
