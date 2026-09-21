<script lang="ts">
	export let id: string | undefined;
	import dayjs from "dayjs";
	import DropdownButton from "../components/misc/DropdownButton.svelte";
	import Loader from "../components/misc/Loader.svelte";
	import Confirm from "../components/modal/Confirm.svelte";
	import Permission from "../components/Permission.svelte";

	import Main from "../components/templates/Main.svelte";
	import request from "../lib/request";
	import app from "../main";

	let applications;
	let limit = 10;

	let modalBody: string;
	let modalVisible = false;
	let modalCb: (arg1: boolean) => void;

	let mode = "All";
	let offset = 0;

	let searchColumn = undefined;
	let searchQuery = undefined;

	const goodPrefixes = [
		"https://twitter.com/",
		"http://twitter.com/",
		"https://www.youtube.com/",
		"http://www.youtube.om/",
		"http://youtube.com/",
		"https://youtube.com/",
		"https://www.roblox.com/",
		"http://www.roblox.com/",
		"http://roblox.com/",
		"https://roblox.com/",
		"https://www.tiktok.com/",
		"https://tiktok.com/",
		"https://www.instagram.com/",
		"https://instagram.com/",
		"https://mobile.twitter.com/",
	];

	const isGoodPrefix = (url) => {
		if (typeof url !== "string") return false;
		url = url.toLowerCase();
		for (const item of goodPrefixes) {
			if (url.startsWith(item)) return true;
		}

		return false;
	};

	const getApplications = async () => {
		let url = `/applications/list?${mode === 'All' ? '' : ('status=' + mode +'&')}offset=${offset}&sort=Desc`;

		if (searchColumn) {
			url = url + '&searchColumn=' + encodeURIComponent(searchColumn);
		}
		if (searchQuery) {
			url = url + '&searchQuery=' + encodeURIComponent(searchQuery);
		}

		const result = await request.get(url);
		return result.data;
	};

	$: {
		if (id !== undefined) {
			applications = null;
			request.get(`/applications/details?id=${id}`).then((res) => {
				applications = [res.data];
			});
		} else {
			getApplications().then((d) => {
				applications = d;
			});
		}
	}
	let errorMessage: string | undefined;

	const rejectApp = async (app, reason) => {
		if (reason === null || reason === "" || reason.length < 3 || reason.length > 128) {
			return;
		}
		request
			.request({
				method: "POST",
				url: `/applications/${app.id}/decline?reason=${encodeURIComponent(reason)}`,
			})
			.then(() => {});
		applications = applications.filter((v) => v.id !== app.id);
	};
</script>

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
		<div class="col-12">
			<h3>Applications</h3>
		</div>
		{#if id === undefined}
			<div class="col-12">
				<div class="row">
					<div class="col-6 col-lg-3">
						<p class="mb-0">Mode</p>
						<select
							class="form-control"
							on:change={(e) => {
								offset = 0;
								mode = e.currentTarget.value;
							}}
						>
							<option value="All">All</option>
							<option value="Approved">Approved</option>
							<option value="Rejected">Rejected</option>
							<option value="SilentlyRejected">Silently Rejected</option>
						</select>
					</div>
					<div class="col-6 col-lg-3">
						<p class="mb-0">Search Column</p>
						<select
							class="form-control"
							bind:value={searchColumn}
						>
							<option value="">All</option>
							<option value="Name">Name</option>
							<option value="About">About</option>
							<option value="Actioner">Actioner</option>
							<option value="DiscordId">Discord Id</option>
							<option value="DiscordUsername">Discord Username</option>
						</select>
					</div>
					<div class="col-6 col-lg-3">
						<p class="mb-0">Search Query</p>
						<input class="form-control dark-input" bind:value={searchQuery} type="text" placeholder="Search Query" />
					</div>
					<div class="col-6 col-lg-3">
						<button class="btn btn-primary" on:click={() => {
							getApplications().then((d) => {
								applications = d;
							});
						}}>Search</button>
					</div>
				</div>
			</div>
		{/if}
		{#if applications && applications.length === 0}
			<div class="col-12">
				{#if searchQuery !== undefined}
					<p class="text-center">There are zero applications matching your search criteria.</p>
					<p class="text-center fst-italics mt-4">You can put the percent sign "%" to find partial matches. For example, "hello%" would show results starting with "hello", while "%hello" would show results ending in "hello".</p>
				{:else}
					<p class="text-center">There are zero applications in this state.</p>
				{/if}
			</div>
		{:else if applications && applications.length}
			<table class="table min-width-1000">
				<thead>
					<tr>
						<th>Actioner</th>
						<th>Submitted</th>
						<th>About</th>
						<th>Discord</th>
						<th>UserID</th>
						{#if mode === "Rejected" || mode === "SilentlyRejected"  || mode === 'All'}
							<th>Rejection Reason</th>
						{/if}
						<th>Actions</th>
					</tr>
				</thead>
				<tbody>
					{#each applications as app}
						<tr>
							<td>{app.authorId}</td>
							<td>{dayjs(app.createdAt).fromNow()}</td>
							<td>{app.about}</td>
							<td>
								<span>ID: {app.discordId}</span>
								<br />
								<span>Username: {app.discordUsername}</span>
							</td>
							<td>{app.userId}</td>

							{#if mode === "SilentlyRejected"}
								<td>Silently rejected.</td>
							{:else if mode === "Rejected" || mode === "All"}
								<td>{app.rejectionReason}</td>
							{/if}
							<td>
								<div class="btn-group w-100">
									<DropdownButton title='Reject'>
										<button class="dropdown-item" on:click={() => {
											rejectApp(app, prompt('Enter the rejection reason, or leave it blank to cancel.', ''));
										}}>Custom Reason</button>
										<button class="dropdown-item" on:click={() => {
											rejectApp(app, 'Sorry, your social media profile is too new or inactive.');
										}}>Too New/Inactive</button>
										<button class="dropdown-item" on:click={() => {
											rejectApp(app, 'Please add the verification phrase to the "about" section of your social media profile.');
										}}>Missing Phrase</button>
										<button class="dropdown-item" on:click={() => {
											rejectApp(app, 'Please use the invite system for creating alternate accounts.');
										}}>Alt Account</button>
										<button class="dropdown-item" on:click={() => {
											rejectApp(app, 'The social media URL specified is invalid or your profile could not be found.');
										}}>Invalid URL</button>
										<button class="dropdown-item" on:click={() => {
											rejectApp(app, 'Please use a public account from a public website such as Roblox or Twitter.');
										}}>Bad Site (Not Public)</button>
										<button class="dropdown-item" on:click={() => {
											rejectApp(app, 'Please do *not* put personal information in your application, such as your age or email address.');
										}}>Private Information</button>
										<button class="dropdown-item" on:click={() => {
											rejectApp(app, 'Please provide more information in your about section.');
										}}>Not Enough Info</button>
									</DropdownButton>

									<button
										class="btn btn-sm btn-outline-danger w-100"
										on:click={() => {
											request
												.request({
													method: "POST",
													url: `/applications/${app.id}/decline-silent`,
												})
												.then(() => {});
											applications = applications.filter((v) => v.id !== app.id);
										}}>Silent Decline</button
									>
									{#if (mode === "Rejected" || mode === "All") && app.discordId && app.rejectionReason && app.rejectionReason.includes("Your application has been declined due to Affiliation with Roblox Condos / Sex Servers.")}
									<a href="/admin/applications/{app.discordId}/analysis" class="btn btn-sm btn-outline-info w-100">View Analysis</a>
								{/if}
									<Permission p="ClearApplications">
											<button
												class="btn btn-sm btn-outline-danger w-100"
												on:click={() => {
													request
														.request({
															method: "POST",
															url: `/applications/${app.id}/clear`,
														})
														.then((data) => {
															applications = applications.map((v) => {
																if (v.id === app.id) {
																	v.matrixName = "[ Content Deleted ]";
																	v.matrixDomain = "[ Content Deleted ]";
																	v.about = "[ Content Deleted ]";
																	v.socialPresence = "[ Content Deleted ]";
																}
																return v;
															});
														});
												}}>Clear</button
											>
									</Permission>
								</div>
							</td>
						</tr>
					{/each}
				</tbody>
			</table>
		{:else if !applications}
			<Loader />
		{/if}
		<div class="col-12">
				<nav>
					<ul class="pagination">
						<li class={`page-item${!offset ? " disabled" : ""}`}>
							<a
								class="page-link"
								href="#!"
								on:click={(e) => {
									e.preventDefault();
									if (offset >= limit) {
										offset -= limit;
										getApplications().then((d) => {
											applications = d;
										});
									}
								}}>Previous</a
							>
						</li>
						<li class="page-item active">
							<a
								class="page-link"
								href="#!"
								on:click={(e) => {
									e.preventDefault();
								}}>{(offset / limit + 1).toLocaleString()}</a
							>
						</li>
						<li class={`page-item${applications && applications.length < limit ? " disabled" : ""}`}>
							<a
								class="page-link"
								href="#!"
								on:click={(e) => {
									e.preventDefault();
									offset += limit;
									getApplications().then((d) => {
										applications = d;
									});
								}}>Next</a
							>
						</li>
					</ul>
				</nav>
			</div>
	</div>
</Main>
