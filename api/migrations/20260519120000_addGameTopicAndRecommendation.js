exports.up = async (knex) => {
    await knex.schema.alterTable('universe', (t) => {
        t.string('topic', 280).nullable().defaultTo(null);
    });
    await knex.schema.createTable('user_game_recommendation', (t) => {
        t.bigIncrements('id').notNullable();
        t.bigInteger('user_id').notNullable();
        t.bigInteger('asset_id').notNullable();
        t.specificType('score', 'double precision').notNullable();
        t.integer('position').notNullable();
        t.dateTime('created_at').notNullable().defaultTo(knex.fn.now());
        t.unique(['user_id', 'asset_id']);
        t.index(['user_id', 'position'], 'user_game_recommendation_user_idx');
    });
};

exports.down = async (knex) => {
    await knex.schema.dropTable('user_game_recommendation');
    await knex.schema.alterTable('universe', (t) => {
        t.dropColumn('topic');
    });
};
