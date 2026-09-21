<script lang="ts">
	import { onDestroy, onMount } from "svelte";
	import Main from "../components/templates/Main.svelte";
	import TelemetryChart from "../components/TelemetryChart.svelte";
	import request from "../lib/request";

	type Point = { Timestamp: string; Value: number };
	type Series = { Name: string; Points: Point[] };
	type RenderPool = {
		WorkerCount: number; IdleWorkerCount: number; RunningJobs: number;
		InteractiveQueuedJobs: number; BackgroundQueuedJobs: number; ConversionQueuedJobs: number;
		AverageRccMilliseconds: number; Ready: boolean;
	};
	type Dashboard = {
		GeneratedAt: string;
		Range: string;
		StepSeconds: number;
		Service: string;
		AvailableServices: string[];
		Summary: Record<string, number | null>;
		Charts: { Key: string; Title: string; Unit: string; Series: Series[] }[];
		RenderPool: RenderPool | null;
	};

	let dashboard: Dashboard | undefined;
	let range = "6h";
	let service = "all";
	let loading = true;
	let error = "";
	let timer: number;

	const summaryCards = [
		["RequestRatePerSecond", "Requests / second", 2],
		["ErrorRatePercent", "Server error rate", 2, "%"],
		["P95RequestDurationMilliseconds", "Request p95", 0, " ms"],
		["P95DatabaseDurationMilliseconds", "Database p95", 0, " ms"],
		["CacheHitRatePercent", "Cache hit rate", 1, "%"],
		["Signups", "Signups in latest window", 0],
		["RobuxVolume", "Robux volume in latest window", 0],
	] as [string, string, number, string?][];

	async function load() {
		loading = true;
		error = "";
		try {
			const response = await request.get<Dashboard>(`/telemetry/dashboard?range=${encodeURIComponent(range)}&service=${encodeURIComponent(service)}`);
			dashboard = response.data;
		} catch (exception) {
			error = exception instanceof Error ? exception.message : "Telemetry is temporarily unavailable.";
		} finally {
			loading = false;
		}
	}

	function format(value: number | null, digits: number, suffix = "") {
		return value == null ? "—" : value.toLocaleString(undefined, { maximumFractionDigits: digits }) + suffix;
	}

	onMount(() => {
		load();
		timer = window.setInterval(load, 30000);
	});
	onDestroy(() => window.clearInterval(timer));
</script>

<svelte:head><title>Telemetry - Admin</title></svelte:head>

<Main>
	<div class="d-flex flex-wrap justify-content-between align-items-end mb-3">
		<div>
			<h1>Telemetry</h1>
			<p class="text-muted mb-0">Operational health across Averia services. Refreshes every 30 seconds.</p>
		</div>
		<div class="d-flex telemetry-filters">
			<label>Service
				<select class="form-control dark-input" bind:value={service} on:change={load}>
					<option value="all">All services</option>
					{#each dashboard?.AvailableServices || [] as name}<option value={name}>{name}</option>{/each}
				</select>
			</label>
			<label>Range
				<select class="form-control dark-input" bind:value={range} on:change={load}>
					<option value="1h">1 hour</option><option value="6h">6 hours</option><option value="24h">24 hours</option>
					<option value="7d">7 days</option><option value="30d">30 days</option>
				</select>
			</label>
			<button class="btn btn-primary" on:click={load} disabled={loading}>Refresh</button>
		</div>
	</div>

	{#if error}<div class="alert alert-danger">{error}</div>{/if}
	{#if loading && !dashboard}<div class="alert alert-secondary">Loading telemetry…</div>{/if}
	{#if dashboard}
		<div class="row">
			{#each summaryCards as card}
				<div class="col-12 col-sm-6 col-xl-3 mb-3"><div class="card mod-card-dark h-100"><div class="card-body">
					<h3>{format(dashboard.Summary[card[0]], card[2], card[3])}</h3><small class="text-muted">{card[1]}</small>
				</div></div></div>
			{/each}
		</div>
		{#if dashboard.RenderPool}
			<h2 class="h4 mt-2">RCC render pool</h2>
			<div class="row">
				<div class="col-6 col-lg-3 mb-3"><div class="card mod-card-dark h-100"><div class="card-body"><h3>{dashboard.RenderPool.WorkerCount}</h3><small class="text-muted">Workers ({dashboard.RenderPool.IdleWorkerCount} idle)</small></div></div></div>
				<div class="col-6 col-lg-3 mb-3"><div class="card mod-card-dark h-100"><div class="card-body"><h3>{dashboard.RenderPool.RunningJobs}</h3><small class="text-muted">Active renders</small></div></div></div>
				<div class="col-6 col-lg-3 mb-3"><div class="card mod-card-dark h-100"><div class="card-body"><h3>{dashboard.RenderPool.InteractiveQueuedJobs + dashboard.RenderPool.BackgroundQueuedJobs + dashboard.RenderPool.ConversionQueuedJobs}</h3><small class="text-muted">Queued renders</small></div></div></div>
				<div class="col-6 col-lg-3 mb-3"><div class="card mod-card-dark h-100"><div class="card-body"><h3>{format(dashboard.RenderPool.AverageRccMilliseconds, 0, " ms")}</h3><small class="text-muted">Average RCC duration</small></div></div></div>
			</div>
		{/if}
		{#if dashboard.Charts.every(chart => chart.Series.every(item => item.Points.length === 0))}
			<div class="alert alert-info">No telemetry was recorded for this service and time range.</div>
		{:else}
			<div class="row">
				{#each dashboard.Charts as chart (chart.Key + dashboard.GeneratedAt)}
					<div class="col-12 col-xl-6 mb-4"><TelemetryChart title={chart.Title} unit={chart.Unit} series={chart.Series.map(item => ({ name: item.Name, points: item.Points }))} /></div>
				{/each}
			</div>
		{/if}
		<p class="text-muted small">Updated {new Date(dashboard.GeneratedAt).toLocaleString()} · {dashboard.StepSeconds}s resolution</p>
	{/if}
</Main>

<style>
	.telemetry-filters { gap: .75rem; align-items: flex-end; }
	.telemetry-filters label { color: #adb5bd; font-size: .8rem; margin: 0; }
	.telemetry-filters select { min-width: 135px; }
	@media (max-width: 767px) { .telemetry-filters { width: 100%; margin-top: 1rem; flex-wrap: wrap; } }
</style>
