<script lang="ts">
	import dayjs from "dayjs";
	import Permission from "../components/Permission.svelte";
	import Main from "../components/templates/Main.svelte";
	import { hasPermission, is } from "../stores/rank";
	import { getElementById } from "../lib/dom";
	import request from "../lib/request";
	let disabled = false;
	let errorMessage: string | undefined;
	let saleErrorMessage: string | undefined;
	let saleType: 'pct' | 'flat' = 'pct';
	let saleAmount: string = '';
	let saleUnits: string = '';
	import * as rank from "../stores/rank";
	import SaleHistory from "../components/SaleHistory.svelte";
	import ProductHistory from "../components/ProductHistory.svelte";
	let queryParams = new URLSearchParams(window.location.search);
	let assetId: number = parseInt(queryParams.get("assetId"), 10) || undefined;
	let dirtyAssetId: string = assetId ? assetId.toString() : ''
	interface IDetailsResponse {
		name: string;
		description: string | null;
		isForSale: boolean;
		isLimited: boolean;
		isLimitedUnique: boolean;
		priceRobux: number | null;
		priceTickets: number|null;
		serialCount: number | null;
		offsaleAt: string | null;
		isOnSale: boolean;
		saleUnitsTotal: number | null;
		saleUnitsRemaining: number | null;
		salePriceRobux: number | null;
		salePriceTix: number | null;
//		hidden: boolean;   [[ will implement at later date - shady ]]
	}
	let assetDetails: Partial<IDetailsResponse> = {};
	let latestFetch;
	$: {
		if (latestFetch) {
			clearTimeout(latestFetch);
		}
		if (assetId) {
			latestFetch = setTimeout(() => {
				disabled = true;
				request
					.get("/product/details?assetId=" + assetId)
					.then((d) => {
						if (d.data.isLimited || d.data.isLimitedUnique) {
							if (!hasPermission('MakeItemLimited')) {
								errorMessage = "You do not have permission to modify limited items.";
								disabled = false;
								return;
							}
						}
						errorMessage = null;
						assetDetails = d.data;
					})
					.finally(() => {
						disabled = false;
					});
			}, 1);
		}
	}

	const refreshAssetDetails = () => {
		return request.get("/product/details?assetId=" + assetId).then(d => {
			assetDetails = d.data;
		});
	}

	const startSale = async () => {
		saleErrorMessage = undefined;
		const units = parseInt(saleUnits, 10);
		const amount = parseInt(saleAmount, 10);
		if (!Number.isSafeInteger(units) || units <= 0) {
			saleErrorMessage = "Enter a positive sales unit count.";
			return;
		}
		if (!Number.isSafeInteger(amount) || amount <= 0) {
			saleErrorMessage = saleType === 'pct' ? "Enter a percent between 1 and 99." : "Enter a positive sale price.";
			return;
		}
		const body: { assetId: number; salesUnits: number; pctOff?: number; flatRobux?: number; flatTix?: number; } = {
			assetId,
			salesUnits: units,
		};
		if (saleType === 'pct') {
			body.pctOff = amount;
		} else {
			body.flatRobux = amount;
		}
		disabled = true;
		try {
			await request.post("/asset/start-sale", body);
			saleAmount = '';
			saleUnits = '';
			await refreshAssetDetails();
		} catch (e) {
			saleErrorMessage = e.message;
		} finally {
			disabled = false;
		}
	}

	const endSale = async () => {
		saleErrorMessage = undefined;
		disabled = true;
		try {
			await request.post("/asset/end-sale", { assetId });
			await refreshAssetDetails();
		} catch (e) {
			saleErrorMessage = e.message;
		} finally {
			disabled = false;
		}
	}
</script>

<style>
	p.err {
		color: red;
	}
</style>

<svelte:head>
	<title>Update Product</title>
</svelte:head>

<Main>
	<div class="row">
		<div class="col-12">
			<h1>Update Product</h1>
			{#if errorMessage}
				<p class="err">{errorMessage}</p>
			{/if}
		</div>
		<div class="col-12">
			<label for="name">AssetID</label>
		</div>
		<div class="col-4">
			<input
				type="text"
				class="form-control"
				id="asset_id"
				{disabled}
				bind:value={dirtyAssetId}
			/>
		</div>
		<div class="col-4">
			<button
			class="btn btn-success"
			disabled={disabled}
			on:click={(e) => {
				assetId = parseInt(dirtyAssetId, 10);
			}}>Search</button>
		</div>
		<div class="col-12">
			{#if assetDetails && assetDetails.name}
				<div class="row">
					<div class="col-12">
						<h2 class="mt-2 mb-2">Editing "{assetDetails.name}"</h2>
					</div>
					<div class="col-2">
						<label for="name">Name</label>
						<input type="text" class="form-control dark-input" id="assetName" {disabled} value={assetDetails.name || ""} />
					</div>
					<div class="col-12">
						<label for="description">Description</label>
						<input type="text" class="form-control dark-input" id="description" {disabled} value={assetDetails.description || ""} />
					</div>
					<div class="col-2">
						<label for="name">R$ Price (Optional)</label>
						<input type="text" class="form-control dark-input" id="priceRobux" {disabled} value={assetDetails.priceRobux || ""} />
					</div>
					<div class="col-2">
						<label for="name">TX$ Price (Optional)</label>
						<input type="text" class="form-control dark-input" id="priceTickets" {disabled} value={assetDetails.priceTickets || ""} />
					</div>
					<div class="col-2 mt-4">
						<label for="is_for_sale">For Sale: </label>
						<input type="checkbox" class="form-check-input" id="is_for_sale" checked={assetDetails.isForSale || false} />
					</div>
				</div>
				<div class="row">
					<Permission p="MakeItemLimited">
						<div class="col-6">
							<label for="description">Limited Status</label>
							<select class="form-control" id="limited-status" value={assetDetails.isLimited ? "limited" : assetDetails.isLimitedUnique ? "limited_u" : "false"}>
								<option value="false">Not Limited</option>
								<option value="limited">Limited</option>
								<option value="limited_u">Limited Unique</option>
							</select>
						</div>
					</Permission>
					<div class="col-6">
						<label for="description">Max Copy Count (optional)</label>
						<input type="text" class="form-control dark-input" id="max-copies" value={assetDetails.serialCount || ""} />
					</div>
					<div class="col-6">
						<label for="description">Offsale Time (EST) (optional)</label>
						<input type="text" class="form-control dark-input" id="offsale-time" placeholder="Format: YYYY-MM-DD HH:MM:SS" value={(assetDetails.offsaleAt && dayjs(assetDetails.offsaleAt).format("YYYY-MM-DD HH:MM:ss")) || ""} />
					</div>
				</div>
			{/if}
		</div>
		<div class="col-12 mt-4">
			<button
				class="btn btn-success"
				disabled={disabled || !assetDetails.name}
				on:click={(e) => {
					e.preventDefault();
					if (disabled) {
						return;
					}
					let offsaleTime = getElementById("offsale-time").value;
					let offsaleDeadline;
					if (offsaleTime) {
						const v = dayjs(offsaleTime, "YYYY-MM-DD HH:MM:SS");
						if (!v.isValid()) {
							errorMessage = `The offsale time specified is not valid. The format is "YYYY-MM-DD HH:MM:SS"`;
							return;
						}
						offsaleDeadline = v.format();
					}

					let isLimited = false;
					let isLimitedUnique = false;
					if (getElementById("limited-status")) {
						let limStatus = getElementById("limited-status").value;
						if (limStatus === "limited" || limStatus === "limited_u") {
							isLimited = true;
						}
						if (limStatus === "limited_u") {
							isLimitedUnique = true;
						}
					}
					let maxSerial = null;
					if (document.getElementById("max-copies")) {
    					let maxSerialValue = document.getElementById("max-copies").value;
    					if (Number.isSafeInteger(parseInt(maxSerialValue, 10))) {
        					maxSerial = parseInt(maxSerialValue, 10);
    				}
				}

					let price = getElementById("priceRobux").value;
					if (Number.isSafeInteger(parseInt(price, 10))) {
						price = parseInt(price, 10);
					}else{
						price = null;
					}

					let priceTickets = getElementById('priceTickets').value;
					if (Number.isSafeInteger(parseInt(priceTickets, 10))) {
						priceTickets = parseInt(priceTickets, 10);
					}else{
						priceTickets = null;
					}

					let description = getElementById('description').value
					if (description === null) {
						description = "No description available."
					}

					let assetName = getElementById('assetName').value
					if (assetName === null) {
						assetName = assetDetails.name
					}
					
					disabled = true;
					request
						.patch("/asset/product", {
							assetId,
							assetName,
							description,
							isForSale: getElementById("is_for_sale").checked,
							maxCopies: maxSerial,
							priceRobux: price,
							priceTickets: priceTickets,
							offsaleDeadline,
							isLimited,
							isLimitedUnique,
						})
						.then((d) => {
							window.location.href = `/catalog/${assetId}/--`;
						})
						.catch((e) => {
							console.log('[error]',e);
							errorMessage = e.message;
						})
						.finally(() => {
							disabled = false;
						});
				}}>Update Product</button
			>
		</div>
		{#if assetDetails && assetDetails.name && is('owner')}
			<div class="col-12 mt-4">
				<hr />
				<h3>Start Sale</h3>
				{#if saleErrorMessage}
					<p class="err">{saleErrorMessage}</p>
				{/if}
				{#if assetDetails.isOnSale}
					<p>
						Sale active &mdash; {assetDetails.saleUnitsRemaining} of {assetDetails.saleUnitsTotal} units remaining.
						{#if assetDetails.salePriceRobux != null}<br />Sale price: R$ {assetDetails.salePriceRobux}{/if}
						{#if assetDetails.salePriceTix != null}<br />Sale price: TX$ {assetDetails.salePriceTix}{/if}
					</p>
					<button class="btn btn-warning" {disabled} on:click={endSale}>End Sale</button>
				{:else}
					<div class="row">
						<div class="col-3">
							<label for="sale-type">Discount Type</label>
							<select id="sale-type" class="form-control" bind:value={saleType}>
								<option value="pct">Percent Off</option>
								<option value="flat">Flat R$ Price</option>
							</select>
						</div>
						<div class="col-3">
							<label for="sale-amount">{saleType === 'pct' ? 'Percent Off' : 'Sale R$ Price'}</label>
							<input id="sale-amount" type="number" class="form-control dark-input" bind:value={saleAmount} />
						</div>
						<div class="col-3">
							<label for="sale-units">Sales (units allowed)</label>
							<input id="sale-units" type="number" class="form-control dark-input" bind:value={saleUnits} />
						</div>
						<div class="col-3 mt-4">
							<button class="btn btn-success" {disabled} on:click={startSale}>Start Sale</button>
						</div>
					</div>
				{/if}
			</div>
		{/if}
		{#if assetDetails}
			<div class="col-12">
				<hr />
				<Permission p="GetSaleHistoryForAsset">
					<ProductHistory assetId={assetId}></ProductHistory>
				</Permission>
				<SaleHistory assetId={assetId}></SaleHistory>
			</div>
		{/if}
	</div>
</Main>

