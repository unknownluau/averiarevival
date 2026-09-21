<script lang="ts">
	import { link } from "svelte-routing";
	import Main from "./templates/Main.svelte";
	import request from "../lib/request";

	export let title: string;
	export let endpoint: string;

	let disabled = false;
	let errorMessage: string | undefined;
	let copiedMessage: string | undefined;
	let input = "";
	let force = "false";
	let didError = false;
	let skipLimitedItems = false;
	let keepLimitedProperties = true;
	let limitedPriceRobux = "";
	let discountedPricePercent = "";
	let skipOpenedOffsaleGiftItems = false;
	let skipOffsaleItems = false;
	let keepOffsaleProperty = false;
	let results: BulkCopyAssetResult[] = [];

	interface BulkCopyAssetResult {
		robloxAssetId: number;
		assetId: number | null;
		catalogUrl: string | null;
		priceRobux: number | null;
		alreadyExisted: boolean;
		success: boolean;
		error: string | null;
	}

	interface BulkCopyAssetResponse {
		results: BulkCopyAssetResult[];
		catalogUrls: string[];
	}

	const parseAssetIds = (): number[] => {
		const matches = input.match(/[0-9]+/g) || [];
		const seen = new Set<number>();
		const ids: number[] = [];
		for (const match of matches) {
			const assetId = parseInt(match, 10);
			if (!Number.isSafeInteger(assetId) || assetId <= 0 || seen.has(assetId)) {
				continue;
			}
			seen.add(assetId);
			ids.push(assetId);
		}
		return ids;
	};

	const submit = async () => {
		if (disabled) {
			return;
		}

		errorMessage = undefined;
		copiedMessage = undefined;
		const assetIds = parseAssetIds();
		if (assetIds.length === 0) {
			errorMessage = "Enter at least one Roblox asset URL or ID.";
			return;
		}

		disabled = true;
		results = [];
		const limitedPrice = parseInt(limitedPriceRobux, 10);
		const discountedPrice = parseInt(discountedPricePercent, 10);
		try {
			const response = await request.post<BulkCopyAssetResponse>(endpoint, {
				assetIds,
				force: force === "true",
				skipLimitedItems,
				keepLimitedProperties,
				limitedPriceRobux: Number.isSafeInteger(limitedPrice) && limitedPrice > 0 ? limitedPrice : null,
				skipOpenedOffsaleGiftItems,
				skipOffsaleItems,
				keepOffsaleProperty,
				discountedPricePercent: Number.isSafeInteger(discountedPrice) && discountedPrice > 0 ? discountedPrice : null,
			});
			results = response.data.results || [];
			didError = results.some((result) => !result.success);
			if (!didError) {
				force = "false";
			}
		} catch (e) {
			errorMessage = e.message;
			didError = true;
		} finally {
			disabled = false;
		}
	};

	const copyCatalogUrls = async () => {
		const urls = results
			.filter((result) => result.success && result.catalogUrl)
			.map((result) => result.catalogUrl)
			.join("\n");
		if (!urls) {
			return;
		}

		await navigator.clipboard.writeText(urls);
		copiedMessage = "Copied catalog URLs.";
	};
</script>

<svelte:head>
	<title>{title}</title>
</svelte:head>

<Main>
	<div class="row">
		<div class="col-12">
			<h1>{title}</h1>
			{#if errorMessage}
				<p class="err">{errorMessage}</p>
			{/if}
			{#if copiedMessage}
				<p class="ok">{copiedMessage}</p>
			{/if}
		</div>
		<div class="col-12">
			<label for="asset-ids">Roblox URLs or IDs</label>
			<textarea
				class="form-control dark-input"
				id="asset-ids"
				rows="10"
				{disabled}
				bind:value={input}
				placeholder="https://www.roblox.com/catalog/17238615/Burro-Pinata"
			/>
		</div>
		<div class="col-3 mt-3">
			{#if didError}
				<label for="force">Force Upload</label>
				<select class="form-control" id="force" bind:value={force}>
					<option value="false">No</option>
					<option value="true">Yes</option>
				</select>
			{/if}
		</div>
		<div class="col-12 mt-3">
			<div class="row">
				<div class="col-3">
					<label for="skip-limited">Don't copy Limited items</label>
					<input
						type="checkbox"
						class="form-check-input ml-2"
						id="skip-limited"
						{disabled}
						bind:checked={skipLimitedItems}
					/>
				</div>
				<div class="col-3">
					<label for="keep-limited">Keep Limited property</label>
					<input
						type="checkbox"
						class="form-check-input ml-2"
						id="keep-limited"
						disabled={disabled || skipLimitedItems}
						bind:checked={keepLimitedProperties}
					/>
				</div>
				<div class="col-3">
					<label for="limited-price">Limited price</label>
					<input
						type="number"
						class="form-control dark-input"
						id="limited-price"
						min="1"
						disabled={disabled || skipLimitedItems}
						bind:value={limitedPriceRobux}
					/>
				</div>
				<div class="col-3">
					<label for="discounted-price">Discounted price percent</label>
					<input
						type="number"
						class="form-control dark-input"
						id="discounted-price"
						min="0"
						{disabled}
						bind:value={discountedPricePercent}
					/>
				</div>
				<div class="col-3">
					<label for="skip-gifts">Don't copy opened offsale gifts</label>
					<input
						type="checkbox"
						class="form-check-input ml-2"
						id="skip-gifts"
						{disabled}
						bind:checked={skipOpenedOffsaleGiftItems}
					/>
				</div>
				<div class="col-3 mt-3">
					<label for="skip-offsale">Don't copy offsale items</label>
					<input
						type="checkbox"
						class="form-check-input ml-2"
						id="skip-offsale"
						{disabled}
						bind:checked={skipOffsaleItems}
					/>
				</div>
				<div class="col-3 mt-3">
					<label for="keep-offsale">Keep Offsale property</label>
					<input
						type="checkbox"
						class="form-check-input ml-2"
						id="keep-offsale"
						disabled={disabled || skipOffsaleItems}
						bind:checked={keepOffsaleProperty}
					/>
				</div>
			</div>
		</div>
		<div class="col-12 mt-4">
			<button class="btn btn-success" {disabled} on:click={submit}>Create Assets</button>
			{#if results.some((result) => result.success && result.catalogUrl)}
				<button class="btn btn-secondary ml-2" {disabled} on:click={copyCatalogUrls}>Copy URLs</button>
			{/if}
		</div>
		{#if results.length}
			<div class="col-12 mt-4">
				<table class="table table-striped table-dark result-table">
					<thead>
						<tr>
							<th>Roblox ID</th>
							<th>Status</th>
							<th>Catalog</th>
							<th>Product</th>
							<th>R$ Price</th>
							<th>Error</th>
						</tr>
					</thead>
					<tbody>
						{#each results as result}
							<tr>
								<td>{result.robloxAssetId}</td>
								<td>
									{#if result.success}
										{result.alreadyExisted ? "Existing" : "Created"}
									{:else}
										Failed
									{/if}
								</td>
								<td>
									{#if result.catalogUrl}
										<a href={result.catalogUrl}>View</a>
									{:else}
										-
									{/if}
								</td>
								<td>
									{#if result.assetId}
										<a use:link href={`/admin/product/update?assetId=${result.assetId}`}>Update</a>
									{:else}
										-
									{/if}
								</td>
								<td>{result.priceRobux ?? "-"}</td>
								<td>{result.error || "-"}</td>
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
	p.ok {
		color: #4ade80;
	}
	.result-table td,
	.result-table th {
		vertical-align: middle;
	}
</style>
