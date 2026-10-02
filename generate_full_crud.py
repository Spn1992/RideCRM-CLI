import os
import glob
import re

models_dir = "src/AmusementRideCRM/Models"
pages_dir = "src/AmusementRideCRM/Pages"

model_files = glob.glob(f"{models_dir}/*.cs")
skip_models = ["ApplicationDbContext", "ErrorLogging", "AuditLog"]

for mf in model_files:
    basename = os.path.basename(mf)
    model_name = basename.replace(".cs", "")

    if model_name in skip_models:
        continue

    with open(mf, "r") as f:
        content = f.read()

    # Try to find primary key name and type
    pk_name = None
    pk_type = "int"
    props = re.findall(r"public\s+([\w\?\[\]]+)\s+(\w+)\s+\{\s+get;\s+set;\s+\}", content)
    for p_type, p_name in props:
        if p_name.lower() == "id" or p_name.lower() == f"{model_name.lower()}id":
            pk_name = p_name
            pk_type = p_type.replace("?", "")
            break
    if not pk_name:
        for p_type, p_name in props:
            if p_name.lower().endswith("id"):
                pk_name = p_name
                pk_type = p_type.replace("?", "")
                break

    if not pk_name:
        continue

    module_dir = os.path.join(pages_dir, model_name)
    os.makedirs(module_dir, exist_ok=True)

    # Generate Details.cshtml
    details_html = f"""@page
@model AmusementRideCRM.Pages.{model_name}.DetailsModel
@{{
    ViewData["Title"] = "{model_name} Details";
}}

<div class="container-fluid">
    <div class="card border-0 shadow-sm rounded-4 mb-4">
        <div class="card-header bg-surface border-bottom py-3">
            <h5 class="mb-0 fw-semibold text-primary-custom">{model_name} Details</h5>
        </div>
        <div class="card-body">
            <dl class="row">
"""
    for p_type, p_name in props:
        details_html += f"""                <dt class="col-sm-3">{p_name}</dt>
                <dd class="col-sm-9">@Model.Item.{p_name}</dd>
"""
    details_html += f"""            </dl>
        </div>
        <div class="card-footer bg-light text-end">
            <a href="/{model_name}/Edit?id=@Model.Item.{pk_name}" class="btn btn-primary">Edit</a>
            <a href="/{model_name}" class="btn btn-secondary">Back to List</a>
        </div>
    </div>
</div>
"""
    with open(os.path.join(module_dir, "Details.cshtml"), "w") as f:
        f.write(details_html)

    # Generate Details.cshtml.cs
    details_cs = f"""using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.{model_name}
{{
    public class DetailsModel : PageModel
    {{
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {{
            _context = context;
        }}

        public AmusementRideCRM.Models.{model_name} Item {{ get; set; }} = default!;

        public async Task<IActionResult> OnGetAsync({pk_type} id)
        {{
            var item = await _context.{model_name}.FirstOrDefaultAsync(m => m.{pk_name} == id);
            if (item == null)
            {{
                return NotFound();
            }}
            Item = item;
            return Page();
        }}
    }}
}}
"""
    with open(os.path.join(module_dir, "Details.cshtml.cs"), "w") as f:
        f.write(details_cs)

    # Generate Edit.cshtml
    edit_html = f"""@page
@model AmusementRideCRM.Pages.{model_name}.EditModel
@{{
    ViewData["Title"] = "Edit {model_name}";
}}

<div class="container-fluid">
    <div class="card border-0 shadow-sm rounded-4 mb-4">
        <div class="card-header bg-surface border-bottom py-3">
            <h5 class="mb-0 fw-semibold text-primary-custom">Edit {model_name}</h5>
        </div>
        <div class="card-body">
            <form method="post">
                <input type="hidden" asp-for="Item.{pk_name}" />
"""
    for p_type, p_name in props:
        if p_name == pk_name or "RowVersion" in p_name: continue
        edit_html += f"""                <div class="mb-3">
                    <label asp-for="Item.{p_name}" class="form-label"></label>
                    <input asp-for="Item.{p_name}" class="form-control" />
                    <span asp-validation-for="Item.{p_name}" class="text-danger"></span>
                </div>
"""
    edit_html += f"""                <div class="text-end mt-4">
                    <button type="submit" class="btn btn-primary">Save</button>
                    <a href="/{model_name}" class="btn btn-secondary">Cancel</a>
                </div>
            </form>
        </div>
    </div>
</div>
"""
    with open(os.path.join(module_dir, "Edit.cshtml"), "w") as f:
        f.write(edit_html)

    # Generate Edit.cshtml.cs
    edit_cs = f"""using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using AmusementRideCRM.Models;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.{model_name}
{{
    public class EditModel : PageModel
    {{
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {{
            _context = context;
        }}

        [BindProperty]
        public AmusementRideCRM.Models.{model_name} Item {{ get; set; }} = default!;

        public async Task<IActionResult> OnGetAsync({pk_type} id)
        {{
            var item = await _context.{model_name}.FirstOrDefaultAsync(m => m.{pk_name} == id);
            if (item == null) return NotFound();
            Item = item;
            return Page();
        }}

        public async Task<IActionResult> OnPostAsync()
        {{
            if (!ModelState.IsValid) return Page();
            _context.Attach(Item).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }}
    }}
}}
"""
    with open(os.path.join(module_dir, "Edit.cshtml.cs"), "w") as f:
        f.write(edit_cs)

    # Generate Create.cshtml
    create_html = f"""@page
@model AmusementRideCRM.Pages.{model_name}.CreateModel
@{{
    ViewData["Title"] = "Create {model_name}";
}}

<div class="container-fluid">
    <div class="card border-0 shadow-sm rounded-4 mb-4">
        <div class="card-header bg-surface border-bottom py-3">
            <h5 class="mb-0 fw-semibold text-primary-custom">Create {model_name}</h5>
        </div>
        <div class="card-body">
            <form method="post">
"""
    for p_type, p_name in props:
        if p_name == pk_name or "RowVersion" in p_name: continue
        create_html += f"""                <div class="mb-3">
                    <label asp-for="Item.{p_name}" class="form-label"></label>
                    <input asp-for="Item.{p_name}" class="form-control" />
                    <span asp-validation-for="Item.{p_name}" class="text-danger"></span>
                </div>
"""
    create_html += f"""                <div class="text-end mt-4">
                    <button type="submit" class="btn btn-primary">Create</button>
                    <a href="/{model_name}" class="btn btn-secondary">Cancel</a>
                </div>
            </form>
        </div>
    </div>
</div>
"""
    with open(os.path.join(module_dir, "Create.cshtml"), "w") as f:
        f.write(create_html)

    # Generate Create.cshtml.cs
    create_cs = f"""using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using AmusementRideCRM.Models;
using System.Threading.Tasks;

namespace AmusementRideCRM.Pages.{model_name}
{{
    public class CreateModel : PageModel
    {{
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {{
            _context = context;
        }}

        [BindProperty]
        public AmusementRideCRM.Models.{model_name} Item {{ get; set; }} = default!;

        public IActionResult OnGet()
        {{
            return Page();
        }}

        public async Task<IActionResult> OnPostAsync()
        {{
            if (!ModelState.IsValid) return Page();
            _context.{model_name}.Add(Item);
            await _context.SaveChangesAsync();
            return RedirectToPage("./Index");
        }}
    }}
}}
"""
    with open(os.path.join(module_dir, "Create.cshtml.cs"), "w") as f:
        f.write(create_cs)
