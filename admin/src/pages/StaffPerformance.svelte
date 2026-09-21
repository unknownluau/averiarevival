<script lang="ts">
	import dayjs from "dayjs";

	class UserInfoSlim {
		id: number;
		username: string;
		description: string;
		created_at: string;
		online_at: string;
		is_moderator: boolean;
		is_admin: boolean;
	}

	export let userId: string;

	import Main from "../components/templates/Main.svelte";
	import {CheckCircleIcon, XCircleIcon} from "svelte-feather-icons";
	import request from "../lib/request";
	let disabled = true;
	let errorMessage: string | undefined;
	let successMessage: string | undefined;
	let title: string | undefined;

	let user: UserInfoSlim | undefined;
	let stats: Record<string, number | string | null> = {
		assets: 0,
		audios: 0,
		signups: 0,
		reports: 0,
		moderatedPlayers: 0,
		permissionGiven: null
	};

	import * as rank from "../stores/rank";
	async function run() {
		await rank.promise;
		if (!rank.hasPermission('GetStaffPerformance')) {
			errorMessage = "You do not have permission to view this page.";
		}

		let req = await request.get("/user?userId=" + encodeURIComponent(userId));
		title = `${req?.data?.username}'s Performance`;
		user = req?.data;

		if (!user || !user?.id) {
			return;
		}

		let stat_table: {variable: string, url: string}[] = [
			{
				variable: "assets",
				url: "totals/assets"
			},
			{
				variable: "audios",
				url: "totals/audios"
			},
			{
				variable: "signups",
				url: "totals/signups"
			},
			{
				variable: "reports",
				url: "totals/reports"
			},
			{
				variable: "moderatedPlayers",
				url: "totals/players-moderated"
			},
		];

		// should be improved but im under a time limit right now so this is how it is
		for (const stat of stat_table) {
			try {
				request.get(`/performance/${stat.url}?userId=${user.id}`).then(d => stats[stat.variable] = d.data);
			} catch (e) {
				disabled = true;
				errorMessage = e.message;
				return;
			}
		}

		try {
			request.get(`/performance/permissions-gave?userId=${user.id}`).then(d => stats["permissionGiven"] = d?.data?.date);
		} catch (e) {
			disabled = true;
			errorMessage = e.message;
			return;
		}

		console.dir(stats);

		disabled = false;
	}

	run().then();
</script>

<svelte:head>
	<title>{title || "Staff Performance"}</title>
</svelte:head>

<Main>
	<div class="row">
		<div class="col-12">
			<h1>{title || "Staff Performance"}</h1>
			{#if errorMessage}
				<p class="err">{errorMessage}</p>
			{/if}
			{#if successMessage}
				<p class="text-success">{successMessage}</p>
			{/if}
		</div>

		{#if !user}
			<div class="row">
				<div class="col-12 text-center">
					<div class="spinner-border sp" />
				</div>
			</div>
		{/if}

		{#if user}
			<div class="card bg-dark overview col-12 mt-4 mb-4">
				<div class="card card-body card-header">
					<h5 class="fw-bold mb-0">Overview</h5>
				</div>
				<div class="col-12 card-body d-flex">
					<div class="ms-2 me-4">
						<a class="ms-0 me-0" href={`/users/${user.id}/profile`}>
							<img
								alt={user.username}
								class="avatar-thumb ms-0 me-0"
								src={`/headshot-thumbnail/image?userId=${encodeURIComponent(user.id)}&width=420&height=420&format=png`}
							/>
						</a>
						<span class="mt-2 col-12 text-white text-center d-inline-block">Staff -
							{#if user.is_admin || user.is_moderator}
							<CheckCircleIcon />
							{:else}
							<XCircleIcon />
							{/if}
						</span>
					</div>
					<div class="flex-grow-1 text-white">
						<div>
							<h1>Total Actions</h1>
							<p style="font-size: 24px;">{stats.assets + stats.audios + stats.signups + stats.reports + stats.moderatedPlayers}</p>
						</div>
						<div class="text-white d-flex flex-wrap justify-content-between">
							<div>
								<h3>Assets Processed</h3>
								<p class="value">{stats.assets}</p>
							</div>
							<div>
								<h3>Audios Processed</h3>
								<p class="value">{stats.audios}</p>
							</div>
							<div>
								<h3>Signups Moderated</h3>
								<p class="value">{stats.signups}</p>
							</div>
							<div>
								<h3>Reports Processed</h3>
								<p class="value">{stats.reports}</p>
							</div>
							<div>
								<h3>Players Moderated</h3>
								<p class="value">{stats.moderatedPlayers}</p>
							</div>
							<div>
								<h3>Earliest Perm.</h3>
								<p class="value">{dayjs(stats.permissionGiven).format("MM/DD/YYYY")} - {dayjs(stats.permissionGiven).fromNow()}</p>
							</div>
						</div>
					</div>
				</div>
			</div>

			<div class="card bg-dark overview col-12 mt-4 mb-4">
				<div class="card card-body card-header">
					<h5 class="fw-bold mb-0">Activity Graphs</h5>
				</div>
				<div class="col-12 card-body text-white justify-content-center">
					<div class="col-12">
						<img
							alt="Work In Progress"
							class="wip avatar-thumb"
							src='/img/9281912c23312bc0d08ab750afa588cc.png'
						/>
					</div>
					<h1 class="text-center">Work In Progress</h1>
					<p class="text-center mb-0">This section hasn't been finished quite yet.</p>
				</div>
			</div>
		{/if}

<!--		<div class="col-12 mt-4">-->
<!--			<button-->
<!--				class="btn btn-success"-->
<!--				{disabled}-->
<!--				on:click={(e) => {-->
<!--					e.preventDefault();-->
<!--					if (disabled) {-->
<!--						return;-->
<!--					}-->

<!--					disabled = true;-->
<!--					request-->
<!--						.post("/lottery/run")-->
<!--						.then((d) => {-->
<!--							successMessage = `Lottery Success! Item ${d.data.name} was given to ${d.data.username}`;-->
<!--						})-->
<!--						.catch((e) => {-->
<!--							errorMessage = e.message;-->
<!--						})-->
<!--						.finally(() => {-->
<!--							disabled = false;-->
<!--						});-->
<!--				}}>Run Lottery</button-->
<!--			>-->
<!--		</div>-->
	</div>
</Main>

<style>
    p.err {
        color: red;
    }
	:global(img.avatar-thumb) {
		border-radius: 50%;
		max-width: 200px;
		min-width: 150px;
	}
	:global(.overview) {
		padding: 12px;
	}
	:global(img.wip) {
		max-width: 250px;
	}
	:global(p.value) {
		font-size: 24px;
		text-align: center;
	}
</style>
