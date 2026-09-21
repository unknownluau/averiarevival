<script lang="ts">
	import Loader from "../components/misc/Loader.svelte";
	import Main from "../components/templates/Main.svelte";
	import request from "../lib/request";
	import { link } from "svelte-routing";

	type PendingUgcRequest = {
		id: number;
		userId: number;
		robloxAssetId: number;
		robloxUrl: string;
		itemName: string | null;
		createdAt: string;
		creatorName: string;
	};

	let pending: PendingUgcRequest[] | null = null;
	let errorMessage = '';

	const load = () => {
		errorMessage = '';
		request.get<PendingUgcRequest[]>('/ugc-requests/pending').then((res) => {
			pending = res.data;
		}).catch((e) => {
			errorMessage = e.message || 'Failed to load pending UGC requests.';
			pending = [];
		});
	};

	load();

	const moderate = (item: PendingUgcRequest, isApproved: boolean) => {
		const verb = isApproved ? 'approve' : 'decline';
		if (!confirm(`Are you sure you want to ${verb} this request?`)) return;

		const original = pending;
		pending = pending ? pending.filter(v => v.id !== item.id) : pending;

		request.post('/ugc-request/moderate', {
			id: item.id,
			isApproved,
		}).then(() => {
			if (pending && pending.length === 0) load();
		}).catch((e) => {
			alert(e.message || `Failed to ${verb} request.`);
			pending = original;
		});
	};
</script>

<svelte:head>
	<title>Pending UGC Items</title>
</svelte:head>

<Main>
	<div class="row">
		<div class="col-12">
			<h1>Pending UGC Items</h1>
			{#if errorMessage}
				<p class="text-danger">{errorMessage}</p>
			{/if}
		</div>
		<div class="col-12 mt-4">
			{#if pending === null}
				<Loader />
			{:else if pending.length === 0}
				<p>There are no pending UGC item requests at this time.</p>
			{:else}
				{#each pending as item}
					<div class="card mod-card-default mb-4">
						<div class="card-body">
							<div class="row">
								<div class="col-12 col-lg-8">
									<h4 class="text-info">{item.itemName || `Asset ${item.robloxAssetId}`}</h4>
									<p class="mb-1">
										Requested by
										<a use:link href={`/admin/manage-user/${item.userId}`}>{item.creatorName}</a>
									</p>
									<p class="mb-1">
										Roblox URL:
										<a href={`https://www.roblox.com/catalog/${item.robloxAssetId}`} target="_blank" rel="noopener noreferrer">https://www.roblox.com/catalog/{item.robloxAssetId}</a>
									</p>
									<p class="mb-0 text-muted small">Submitted: {new Date(item.createdAt).toLocaleString()}</p>
								</div>
								<div class="col-12 col-lg-4">
									<div class="btn-group w-100 mt-2">
										<button
											class="btn btn-success border border-dark"
											on:click={() => moderate(item, true)}
										>Approve</button>
										<button
											class="btn btn-danger border border-dark"
											on:click={() => moderate(item, false)}
										>Decline</button>
									</div>
								</div>
							</div>
						</div>
					</div>
				{/each}
			{/if}
		</div>
	</div>
</Main>
