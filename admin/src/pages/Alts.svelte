<script lang="ts">
	import { link } from "svelte-routing";
	import { chunk } from "lodash";
	import Confirm from "../components/modal/Confirm.svelte";
	import Main from "../components/templates/Main.svelte";
	import request from "../lib/request";

	let groups: any[] = [];
	let limit = 50;
	let offset = 0;
	let loading = false;

	let modalBody: string | undefined;
	let modalCb: (didClickYes: boolean) => void | undefined;
	let modalVisible = false;

	let activeGroup: any = null;
	let banReason = "";
	let banInternalReason = "";

	const load = async () => {
		loading = true;
		try {
			const res = await request.get(
				"/alt-accounts/by-mac?limit=" + limit + "&offset=" + offset
			);
			groups = res.data.map((g: any) => {
				g.expanded = false;
				g.users = g.users.map((u: any) => {
					u.checked = false;
					return u;
				});
				return g;
			});
		} catch (e) {
			alert("Error loading alt accounts: " + e.message);
		} finally {
			loading = false;
		}
	};

	$: {
		limit;
		offset;
		load();
	}

	const toggleExpand = (idx: number) => {
		groups[idx].expanded = !groups[idx].expanded;
		groups = groups;
	};

	const setAllChecked = (idx: number, checked: boolean) => {
		groups[idx].users = groups[idx].users.map((u: any) => {
			u.checked = checked;
			return u;
		});
		groups = groups;
	};

	const banGroup = async () => {
		if (!activeGroup) return;
		const targets = activeGroup.users.filter((u: any) => u.checked);
		const batches = chunk(targets, 100);
		for (const batch of batches) {
			const promises = batch.map((u: any) =>
				request.request({
					method: "POST",
					url: "ban",
					data: {
						userId: u.id,
						reason: banReason,
						internalReason: banInternalReason,
					},
				})
			);
			await Promise.all(promises);
		}
	};

	const openBanModal = (g: any) => {
		const selected = g.users.filter((u: any) => u.checked);
		if (selected.length === 0) return;
		activeGroup = g;
		modalBody =
			"Confirm that you want to mass ban these users: " +
			selected
				.map((u: any) => u.username + " (ID = " + u.id + ")")
				.join(", ");
		modalCb = async (t: boolean) => {
			if (!t) return;
			if (!banReason) {
				alert("Please specify a reason.");
				return;
			}
			if (!banInternalReason) {
				alert("Please specify an internal reason.");
				return;
			}
			try {
				await banGroup();
			} catch (e) {
				alert("Error mass-banning: " + e.message);
				return;
			}
			window.location.reload();
		};
		modalVisible = true;
	};
</script>

<style>
	code {
		font-size: 0.95em;
	}
	a {
		text-decoration: none;
	}
	tr.mac-row {
		cursor: pointer;
	}
</style>

<svelte:head>
	<title>Possible Alt Accounts</title>
</svelte:head>

<Main>
	{#if modalVisible}
		<Confirm
			title="Confirm"
			message={modalBody}
			cb={(e) => {
				modalVisible = false;
				modalCb(e);
			}}
		/>
	{/if}
	<div class="row">
		<div class="col-12 col-md-8">
			<h1>Possible Alt Accounts</h1>
			<p class="text-muted mb-2">
				Groups of users that share the same MAC address. Click a row to expand. Each group has its own mass-ban form.
			</p>
		</div>
		<div class="col-12 col-md-2">
			<label for="limit">LIMIT</label>
			<select id="limit" class="form-control" disabled={loading} on:change={(e) => {
				limit = parseInt(e.currentTarget.value, 10);
				offset = 0;
			}}>
				<option value="50">50</option>
				<option value="100">100</option>
				<option value="200">200</option>
			</select>
		</div>
		<div class="col-12 col-md-2">
			<p class="mb-0 mt-0">&emsp;</p>
			<button class="btn btn-primary w-100" disabled={loading} on:click={load}>Reload</button>
		</div>

		<div class="col-12 mt-3">
			{#if loading}
				<p>Loading...</p>
			{:else if groups.length === 0}
				<p>No alt account groups found.</p>
			{:else}
				<table class="table">
					<thead>
						<tr>
							<th style="width: 40px;"></th>
							<th>MAC Address</th>
							<th>Users</th>
						</tr>
					</thead>
					<tbody>
						{#each groups as g, idx}
							<tr class="mac-row" on:click={() => toggleExpand(idx)}>
								<td>
									<span class="badge bg-secondary">
										{g.expanded ? "-" : "+"}
									</span>
								</td>
								<td><code>{g.macAddress}</code></td>
								<td>Found <strong>{g.userCount}</strong> users with same MAC Address</td>
							</tr>
							{#if g.expanded}
								<tr>
									<td></td>
									<td colspan="2">
										<div class="card">
											<div class="card-body">
												<div class="mb-2">
													<input type="checkbox" class="form-check-input me-2" id={"selall-" + idx} checked={g.users.length > 0 && g.users.every((u) => u.checked)} on:change={(e) => setAllChecked(idx, e.currentTarget.checked)} />
													<label for={"selall-" + idx}>Select all</label>
												</div>
												<table class="table table-sm">
													<thead>
														<tr>
															<th></th>
															<th>ID</th>
															<th>Username</th>
															<th>Status</th>
															<th>Actions</th>
														</tr>
													</thead>
													<tbody>
														{#each g.users as u}
															<tr>
																<td>
																	<input type="checkbox" class="form-check-input" bind:checked={u.checked} />
																</td>
																<td><a use:link href={"/admin/manage-user/" + u.id}>{u.id}</a></td>
																<td><a use:link href={"/admin/manage-user/" + u.id}>{u.username}</a></td>
																<td>
																	{#if u.status === "Ok"}
																		<span class="badge bg-success">OK</span>
																	{:else if u.status === "Deleted" || u.status === "Forgotten"}
																		<span class="badge bg-danger">{u.status}</span>
																	{:else}
																		<span class="badge bg-warning">{u.status}</span>
																	{/if}
																</td>
																<td>
																	<a use:link class="btn btn-sm btn-outline-danger" href={"/admin/ban-user/" + u.id}>Ban</a>
																</td>
															</tr>
														{/each}
													</tbody>
												</table>
												<div class="mt-2">
													<p class="mb-1 fw-bold">Mass Ban ({g.users.filter((u) => u.checked).length})</p>
													<input bind:value={banReason} class="form-control mb-1" placeholder="Ban Reason" on:focus={() => activeGroup = g} />
													<textarea bind:value={banInternalReason} class="form-control mb-1" rows={2} placeholder="Internal Reason" on:focus={() => activeGroup = g}></textarea>
													<button class="btn btn-sm btn-outline-danger" disabled={!g.users.some((u) => u.checked)} on:click={() => openBanModal(g)}>Ban Selected</button>
												</div>
											</div>
										</div>
									</td>
								</tr>
							{/if}
						{/each}
					</tbody>
				</table>
			{/if}
		</div>

		<div class="col-12">
			<nav>
				<ul class="pagination">
					<li class={"page-item" + (loading || !offset ? " disabled" : "")}>
						<a class="page-link" href="#!" on:click={(e) => {
							e.preventDefault();
							if (offset >= limit) offset -= limit;
						}}>Previous</a>
					</li>
					<li class="page-item active">
						<a class="page-link" href="#!" on:click={(e) => e.preventDefault()}>{(offset / limit + 1).toLocaleString()}</a>
					</li>
					<li class={"page-item" + (loading || groups.length < limit ? " disabled" : "")}>
						<a class="page-link" href="#!" on:click={(e) => {
							e.preventDefault();
							offset += limit;
						}}>Next</a>
					</li>
				</ul>
			</nav>
		</div>
	</div>
</Main>
