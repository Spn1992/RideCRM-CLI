import os
import glob
import re

# Get all model files
models_dir = "src/AmusementRideCRM/Models"
pages_dir = "src/AmusementRideCRM/Pages"

model_files = glob.glob(f"{models_dir}/*.cs")

# Basic models to skip or that are infra/special (we can skip DbContext and some others)
skip_models = ["ApplicationDbContext", "ErrorLogging", "AuditLog"]

for mf in model_files:
    basename = os.path.basename(mf)
    model_name = basename.replace(".cs", "")

    if model_name in skip_models:
        continue

    with open(mf, "r") as f:
        content = f.read()

    # Try to find primary key name
    pk = None
    props = re.findall(r"public\s+[\w\?\[\]]+\s+(\w+)\s+\{\s+get;\s+set;\s+\}", content)
    for p in props:
        if p.lower() == "id" or p.lower() == f"{model_name.lower()}id":
            pk = p
            break
    if not pk:
        for p in props:
            if p.lower().endswith("id"):
                pk = p
                break

    if not pk:
        continue # Can't generate CRUD without a PK easily in this simple generator

    # We will generate a basic list (Index) view for each model in its own folder
    module_dir = os.path.join(pages_dir, model_name)
    os.makedirs(module_dir, exist_ok=True)

    # Generate Index.cshtml
    index_html = f"""@page
@model AmusementRideCRM.Pages.{model_name}.IndexModel
@{{
    ViewData["Title"] = "{model_name} List";
}}

<div class="container-fluid">
    <div class="card border-0 shadow-sm rounded-4 mb-4">
        <div class="card-header bg-surface border-bottom d-flex justify-content-between align-items-center py-3">
            <h5 class="mb-0 fw-semibold text-primary-custom">{model_name} Management</h5>
            <a href="/{model_name}/Create" class="btn btn-primary btn-sm"><i class="bi bi-plus-lg me-1"></i> Create New</a>
        </div>
        <div class="card-body p-0">
            <div class="table-responsive">
                <table class="table table-hover table-custom mb-0">
                    <thead class="bg-light">
                        <tr>
                            <th class="ps-4">ID</th>
"""
    # Pick a few string properties for the table
    display_props = []
    for p in props:
        if p != pk and "Version" not in p and "Id" not in p and len(display_props) < 4:
            display_props.append(p)
            index_html += f"                            <th>{p}</th>\n"

    index_html += """                            <th class="text-end pe-4">Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        @foreach (var item in Model.Items)
                        {
                            <tr>
                                <td class="ps-4">@item.""" + pk + """</td>
"""
    for p in display_props:
        index_html += f"                                <td>@item.{p}</td>\n"

    index_html += f"""                                <td class="text-end pe-4">
                                    <a href="/{model_name}/Details?id=@item.{pk}" class="btn btn-sm btn-light"><i class="bi bi-eye"></i></a>
                                    <a href="/{model_name}/Edit?id=@item.{pk}" class="btn btn-sm btn-light"><i class="bi bi-pencil"></i></a>
                                </td>
                            </tr>
                        }}
                    </tbody>
                </table>
            </div>
        </div>
    </div>
</div>
"""

    with open(os.path.join(module_dir, "Index.cshtml"), "w") as f:
        f.write(index_html)

    index_cs = f"""using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.{model_name}
{{
    public class IndexModel : PageModel
    {{
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {{
            _context = context;
        }}

        public IList<{model_name}> Items {{ get; set; }} = default!;

        public async Task OnGetAsync()
        {{
            if (_context.{model_name} != null)
            {{
                Items = await _context.{model_name}.Take(100).ToListAsync();
            }}
        }}
    }}
}}
"""
    with open(os.path.join(module_dir, "Index.cshtml.cs"), "w") as f:
        f.write(index_cs)
