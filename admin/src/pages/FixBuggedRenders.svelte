<script lang="ts">
	import Main from "../components/templates/Main.svelte";
	import request from "../lib/request";
	import * as rank from "../stores/rank";

	let disabled = false;
	let limit = 100;
	let newestFirst = false;
	let ownership = "roblox";
	let errorMessage: string | undefined;
	let matchedCount: number | undefined;
	let rerenderedAssetIds: number[] = [];

	rank.promise.then(() => {
		if (!rank.hasPermission("RequestAssetReRender")) {
			errorMessage = "You do not have permission to request asset re-renders.";
		}
	});

	const submit = async () => {
		if (disabled) return;

		disabled = true;
		errorMessage = undefined;
		matchedCount = undefined;
		rerenderedAssetIds = [];

		try {
			const response = await request.post("/asset/fix-bugged-renders", { limit, newestFirst, ownership });
			matchedCount = response.data.matchedCount;
			rerenderedAssetIds = response.data.rerenderedAssetIds || [];
		} catch (e) {
			errorMessage = e.message;
		} finally {
			disabled = false;
		}
	};
</script>

<svelte:head>
	<title>Fix Bugged Renders</title>
</svelte:head>

<Main>
	<div class="row">
		<div class="col-12">
			<h1>Fix Bugged Renders</h1>
			{#if errorMessage}
				<p class="err">{errorMessage}</p>
			{/if}
		</div>

		<div class="col-12 col-md-4">
			<label for="limit">Batch limit</label>
			<input
				id="limit"
				type="number"
				min="1"
				max="500"
				class="form-control dark-input"
				bind:value={limit}
				disabled={disabled}
			/>
		</div>

		<div class="col-12 col-md-4">
			<label for="ownership">Asset ownership</label>
			<select id="ownership" class="form-control dark-input" bind:value={ownership} disabled={disabled}>
				<option value="roblox">ROBLOX owned</option>
				<option value="user">User owned</option>
			</select>
		</div>

		<div class="col-12 col-md-4">
			<label for="sort">Search order</label>
			<select id="sort" class="form-control dark-input" bind:value={newestFirst} disabled={disabled}>
				<option value={false}>Oldest bugged items</option>
				<option value={true}>Newest bugged items</option>
			</select>
		</div>

		<div class="col-12 mt-4">
			<button class="btn btn-success" disabled={disabled} on:click|preventDefault={submit}>
				Queue Re-Renders
			</button>
		</div>

		{#if matchedCount !== undefined}
			<div class="col-12 mt-4">
				<div class="alert alert-info">
					Queued {matchedCount} {ownership === "roblox" ? "ROBLOX-owned" : "user-owned"} approved catalog
					item{matchedCount === 1 ? "" : "s"} for re-render.
				</div>
			</div>
		{/if}

		{#if rerenderedAssetIds.length > 0}
			<div class="col-12 mt-2">
				<table class="table table-dark table-striped">
					<thead>
						<tr>
							<th>Asset ID</th>
							<th>Catalog</th>
						</tr>
					</thead>
					<tbody>
						{#each rerenderedAssetIds as assetId}
							<tr>
								<td>{assetId}</td>
								<td><a href={`/catalog/${assetId}/--`} target="_blank" rel="noreferrer">Open</a></td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		{/if}
	</div>
</Main>

<style>
	p.err {
		color: red;
	}
</style>
