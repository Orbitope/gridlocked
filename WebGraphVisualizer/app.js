document.getElementById('loadGraphBtn').addEventListener('click', loadData);

async function loadData() {
    try {
        const response = await fetch('../Exports/sample_graph.json');
        const data = await response.json();
        processGraphData(data);
    } catch (e) {
        console.error("Failed to load graph JSON. Ensure you are running a local web server.", e);
        alert("Failed to load sample_graph.json. Are you running a local HTTP server?");
    }
}

function processGraphData(data) {
    const nodes = data.nodes;
    const startState = data.startState;
    
    document.getElementById('nodeCount').innerText = nodes.length;
    
    let totalEdges = 0;
    const adjMap = new Map();
    
    nodes.forEach(n => {
        totalEdges += n.edges.length;
        adjMap.set(n.id, n.edges);
    });
    
    document.getElementById('edgeCount').innerText = totalEdges;
    
    // Compute Depths using BFS
    const depths = new Map();
    const queue = [{id: startState, d: 0}];
    depths.set(startState, 0);
    
    let maxDepth = 0;
    
    while(queue.length > 0) {
        const curr = queue.shift();
        const neighbors = adjMap.get(curr.id) || [];
        
        neighbors.forEach(nx => {
            if(!depths.has(nx)) {
                depths.set(nx, curr.d + 1);
                maxDepth = Math.max(maxDepth, curr.d + 1);
                queue.push({id: nx, d: curr.d + 1});
            }
        });
    }
    
    document.getElementById('shortestPath').innerText = maxDepth;
    
    drawHourglass(depths, maxDepth);
    drawSpine(adjMap, depths, startState);
}

function drawHourglass(depths, maxDepth) {
    const container = d3.select("#hourglassViz");
    container.selectAll("*").remove();
    
    const width = container.node().getBoundingClientRect().width;
    const height = container.node().getBoundingClientRect().height;
    const margin = {top: 20, right: 30, bottom: 30, left: 40};
    
    const svg = container.append("svg")
        .attr("width", width)
        .attr("height", height);
        
    // Calculate states per depth
    const counts = new Array(maxDepth + 1).fill(0);
    for(let [id, d] of depths.entries()) {
        counts[d]++;
    }
    
    const chartData = counts.map((count, depth) => ({depth, count}));
    
    const x = d3.scaleLinear()
        .domain([0, maxDepth])
        .range([margin.left, width - margin.right]);
        
    const maxCount = d3.max(counts);
    
    const y = d3.scaleLinear()
        .domain([-maxCount, maxCount]) // Mirrored
        .range([height - margin.bottom, margin.top]);
        
    // Gradient definition
    const defs = svg.append("defs");
    const gradient = defs.append("linearGradient")
        .attr("id", "area-gradient")
        .attr("x1", "0%").attr("y1", "0%")
        .attr("x2", "100%").attr("y2", "0%");
        
    gradient.append("stop").attr("offset", "0%").attr("stop-color", "#3b82f6");
    gradient.append("stop").attr("offset", "50%").attr("stop-color", "#8b5cf6");
    gradient.append("stop").attr("offset", "100%").attr("stop-color", "#ef4444");

    const area = d3.area()
        .curve(d3.curveMonotoneX)
        .x(d => x(d.depth))
        .y0(d => y(-d.count))
        .y1(d => y(d.count));
        
    svg.append("path")
        .datum(chartData)
        .attr("class", "area")
        .attr("d", area);
        
    // Axes
    svg.append("g")
        .attr("class", "axis")
        .attr("transform", `translate(0,${height/2})`)
        .call(d3.axisBottom(x).ticks(10));
}

function drawSpine(adjMap, depths, startState) {
    const container = d3.select("#spineViz");
    container.selectAll("*").remove();
    
    const width = container.node().getBoundingClientRect().width;
    const height = container.node().getBoundingClientRect().height;
    
    const svg = container.append("svg")
        .attr("width", width)
        .attr("height", height);
        
    // Prepare force graph data
    // To prevent 100,000 nodes from crashing the browser, we sample
    const nodes = [];
    const links = [];
    
    for(let [id, edges] of adjMap.entries()) {
        const d = depths.get(id);
        if(d !== undefined) {
            nodes.push({id: id, group: d});
            edges.forEach(target => {
                if(depths.has(target) && depths.get(target) === d + 1) { // Only draw forward edges to make it tree-like
                    links.push({source: id, target: target});
                }
            });
        }
    }
    
    // Super basic sampling if too big
    let displayNodes = nodes;
    let displayLinks = links;
    if(nodes.length > 1000) {
        displayNodes = nodes.filter(n => Math.random() > 0.5 || n.id === startState);
        const nodeSet = new Set(displayNodes.map(n => n.id));
        displayLinks = links.filter(l => nodeSet.has(l.source) && nodeSet.has(l.target));
    }
    
    const simulation = d3.forceSimulation(displayNodes)
        .force("link", d3.forceLink(displayLinks).id(d => d.id).distance(10))
        .force("charge", d3.forceManyBody().strength(-5))
        .force("center", d3.forceCenter(width / 2, height / 2))
        .force("x", d3.forceX(d => (d.group * width) / Math.max(1, d3.max(displayNodes, n => n.group))).strength(1));

    const link = svg.append("g")
        .attr("class", "links")
        .selectAll("line")
        .data(displayLinks)
        .enter().append("line")
        .attr("class", "link");

    const node = svg.append("g")
        .attr("class", "nodes")
        .selectAll("circle")
        .data(displayNodes)
        .enter().append("circle")
        .attr("class", "node")
        .attr("r", 4)
        .attr("fill", d => d3.interpolateCool(d.group / Math.max(1, d3.max(displayNodes, n => n.group))));

    simulation.on("tick", () => {
        link
            .attr("x1", d => d.source.x)
            .attr("y1", d => d.source.y)
            .attr("x2", d => d.target.x)
            .attr("y2", d => d.target.y);
        node
            .attr("cx", d => d.x)
            .attr("cy", d => d.y);
    });
}
