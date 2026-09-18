"""Generate explicit, compiled bindings from a reviewed allowlist, not runtime reflection dispatch."""
from pathlib import Path
import json,re,sys
from cache_reference import fetch,BASE,ROOT

bindings=[]
def add(category,name,module,expression,shape='doc',paged=False,description=None,apis=None):
    bindings.append(dict(category=category,name='topsolid_'+name,module=module,expression=expression,shape=shape,paged=paged,description=description or name.replace('_',' ').capitalize()+'.',apis=apis))

add('Documents','list_documents','kernel','TopSolidHost.Documents.GetOpenDocuments()','none',True,'List open documents. IDs identify exact revisions.')
add('Documents','list_loaded_documents','kernel','TopSolidHost.Documents.GetDocuments()','none',True)
add('Documents','get_document_references','kernel','TopSolidHost.Documents.GetReferencedDocuments(a.Document(p), false)',paged=True)
add('Documents','list_document_properties','kernel','TopSolidHost.Documents.GetProperties(a.Document(p))',paged=True)
add('Pdm','list_projects','kernel','TopSolidHost.Pdm.GetProjects(true, false)','none',True,'List working projects with friendly name and pdmObjectId in each item. Names are included; do not fetch each object again. Use limit=100 and follow hasMore with offset to list all. Libraries have a separate tool.')
add('Pdm','list_libraries','kernel','TopSolidHost.Pdm.GetProjects(false, true)','none',True,'List library projects with friendly name and pdmObjectId in each item. Names are included; do not fetch each object again. Use limit=100 and follow hasMore with offset to list all.')
add('Pdm','get_current_project','kernel','TopSolidHost.Pdm.GetCurrentProject()','none')
add('Pdm','get_selected_pdm_objects','kernel','TopSolidHost.Pdm.GetSelectedPdmObjectIds()','none',True)
add('Pdm','search_projects','kernel','TopSolidHost.Pdm.SearchProjectByName((string)p["name"])','name',True)
add('Pdm','search_documents','kernel','TopSolidHost.Pdm.SearchDocumentByName(a.Pdm(p), (string)p["name"])','pdmname',True)
add('Pdm','list_major_revisions','kernel','TopSolidHost.Pdm.GetMajorRevisions(a.Pdm(p))','pdm',True)
add('Pdm','list_minor_revisions','kernel','TopSolidHost.Pdm.GetMinorRevisions(new PdmMajorRevisionId((string)p["majorRevisionId"]))','major',True)
add('Pdm','resolve_pdm_document','kernel','TopSolidHost.Documents.GetDocument(a.Pdm(p))','pdm')
add('Entities','list_elements','kernel','TopSolidHost.Elements.GetElements(a.Document(p))',paged=True)
add('Entities','find_element','kernel','TopSolidHost.Elements.SearchByName(a.Document(p), (string)p["name"])','docname',description='Find an element with a unique name. Elements without unique names are not found by this API.')
add('Entities','list_parameters','kernel','TopSolidHost.Parameters.GetParameters(a.Document(p))',paged=True,description='List parameter entity IDs from the native parameters folder only. Prefer list_parameter_values for names and typed values in one call.')
add('Entities','list_functions','kernel','TopSolidHost.Entities.GetFunctions(a.Document(p))',paged=True)
add('Entities','list_publishings','kernel','TopSolidHost.Entities.GetPublishings(a.Document(p))',paged=True)
for dimension in (2,3):
    category='Sketch'+str(dimension)+'D'; service='TopSolidHost.Sketches'+str(dimension)+'D'
    add(category,'list_sketches'+str(dimension)+'d','kernel',service+'.GetSketches(a.Document(p))',paged=True)
    for topology in ('Profiles','Segments','Vertices'):
        add(category,'list_sketch'+str(dimension)+'d_'+topology.lower(),'kernel',service+'.Get'+topology+'(a.Element(p))','element',True)
    for suffix,method,paged in [('vertex_point','GetVertexPoint',False),('profile_segments','GetProfileSegments',True),('profile_closed','IsProfileClosed',False),('segment_curve_type','GetSegmentCurveType',False)]:
        add(category,'get_sketch'+str(dimension)+'d_'+suffix,'kernel',service+'.'+method+'(a.Item(p))','item',paged)
    add('Design'+str(dimension)+'D','list_points'+str(dimension)+'d','kernel','TopSolidHost.Geometries'+str(dimension)+'D.GetPoints(a.Document(p))',paged=True)
    add('Design'+str(dimension)+'D','get_point'+str(dimension)+'d','kernel','TopSolidHost.Geometries'+str(dimension)+'D.GetPointGeometry(a.Element(p))','element',description='Read point coordinates in metres.')
add('Design3D','list_shapes','kernel','TopSolidHost.Shapes.GetShapes(a.Document(p))',paged=True)
for suffix,method,shape in [('shape_volume','GetShapeVolume','element'),('shape_type','GetShapeType','element'),('shape_vertex_point','GetVertexPoint','item'),('face_area','GetFaceArea','item'),('edge_curve_type','GetEdgeCurveType','item')]:
    add('Design3D','get_'+suffix,'kernel','TopSolidHost.Shapes.'+method+'(a.'+('Item' if shape=='item' else 'Element')+'(p))',shape,description='Read '+suffix.replace('_',' ')+'. Length, area and volume use SI metres, square metres and cubic metres.')
for topology in ('Faces','Edges','Vertices'):
    add('Design3D','list_shape_'+topology.lower(),'kernel','TopSolidHost.Shapes.Get'+topology+'(a.Element(p))','element',True)
add('Assembly','list_assembly_parts','cad','TopSolidDesignHost.Assemblies.GetParts(a.Document(p))',paged=True)
add('Assembly','get_occurrence_document','cad','TopSolidDesignHost.Assemblies.GetOccurrenceDefinition(a.Element(p))','element')
add('Assembly','get_occurrence_transform','kernel','TopSolidHost.Geometries3D.GetOccurrenceDefinitionTransform(a.Element(p))','element')
add('Tooling','get_part_material','cad','TopSolidDesignHost.Parts.GetMaterial(a.Document(p))')
add('Tooling','get_base_document','cad','TopSolidDesignHost.Tools.GetBaseDocument(a.Document(p))')
add('Drafting','list_drafting_views','drafting','TopSolidDraftingHost.Draftings.GetDraftingViews(a.Document(p))',paged=True)
add('Drafting','get_drafting_view_title','drafting','TopSolidDraftingHost.Draftings.GetViewTitle(a.Element(p))','element')
add('Electrode','list_electrodes','electrode','TopSolidElectrodeHost.Electrodes.GetElectrodes(a.Document(p), false)',paged=True)
add('Electrode','list_electrode_mandrels','electrode','TopSolidElectrodeHost.Electrodes.GetElectrodeMandrels(a.Element(p))','element',True)
add('Cam/PartSetup','list_cam_parts','cam','TopSolidCamHost.Documents.GetParts(a.Document(p))',paged=True)
add('Cam/PartSetup','is_part_setup_document','cam','TopSolidCamHost.PartSettingDocuments.IsPartSetting(a.Document(p))')
add('Cam/Machine','get_cam_machine','cam','TopSolidCamHost.Documents.GetMachine(a.Document(p))')
for kind in ('ToolHolders','PartHolders','Magazines','Pockets'):
    snake=re.sub(r'(?<!^)(?=[A-Z])','_',kind).lower()
    add('Cam/Machine','list_machine_'+snake,'cam','TopSolidCamHost.Machines.Get'+kind+'(a.Element(p))','element',True)
add('Cam/Operation','list_cam_operations','cam','TopSolidCamHost.Operations.GetOperations(a.Document(p))',paged=True)
add('Cam/Operation','list_cam_scenario','cam','TopSolidCamHost.Operations.GetScenarioOperations(a.Document(p))',paged=True)
add('Cam/Operation','list_cam_parameters','cam','TopSolidCamHost.Parameters.GetParameters(a.CamElement(p))','camElement',True,'Inspect all parameters INSIDE a CAM operation with friendly names, category, current typed values, exact SI unit type, native enum choices and edit support. Default limit=100; follow nextOffset until hasMore=false, including when the output budget shortens a page. Optional category=CuttingConditions narrows to operation cutting conditions. Inspect every category when finding machining/tool/strategy settings; no cutting-condition document lookup is needed.')
add('Cam/CuttingConditions','get_cutting_conditions_document','cam','TopSolidCamHost.Operations.GetCurrentCuttingConditionsDocument(a.Element(p))','element',description='Read the linked cutting-condition LIBRARY DOCUMENT only when explicitly requested. For ordinary cutting conditions, speeds, feeds or operation edits use list_cam_parameters and get_cam_parameter_value.')
add('Cam/CuttingConditions','get_cutting_conditions_abacus','cam','TopSolidCamHost.Operations.GetCurrentCuttingConditionsAbacus(a.Element(p))','element',description='Read the linked cutting-condition LIBRARY ABACUS only when explicitly requested. For ordinary operation cutting conditions use list_cam_parameters.')
add('Cam/CuttingConditions','list_cutting_conditions_documents','cam','TopSolidCamHost.Operations.GetAllCuttingConditionsDocuments(a.Element(p))','element',True,'List available cutting-condition LIBRARY DOCUMENTS only when explicitly requested. An empty list does not mean the operation has no cutting conditions; inspect list_cam_parameters instead.')
add('Cam/Postprocessor','get_postprocessor_id','cam','TopSolidCamHost.NCPostProcessor.GetNCPostProcessorId(a.Document(p))')
add('Cam/Nc','list_nc_files','cam','TopSolidCamHost.NCFiles.GetNCFiles(a.Document(p))',paged=True)
add('Cam/Nc','list_cam_programs','cam','TopSolidCamHost.Programs.GetPrograms(a.Document(p))',paged=True)
add('Tooling','list_cam_tools','cam','TopSolidCamHost.Documents.GetTools(a.Document(p), false)',paged=True)
add('Cam/PartSetup','list_cam_coordinate_systems','cam','TopSolidCamHost.Documents.GetWCSs(a.Document(p))',paged=True)
add('Cam/Simulation','is_simulation_complete','cam','TopSolidCamHost.Simulation.IsSimulationComplete()','none')
add('Cae','list_cae_measures','cae','TopSolidCaeHost.Results.GetMeasures(a.Document(p))',paged=True)

catalog=json.loads((ROOT/'docs/api/assemblies-7.20.json').read_text(encoding='utf-8-sig'))
types={t['name']:t for asm in catalog for t in asm['types']}
hosttypes={name.split('.')[-1]:t for name,t in types.items() if name.endswith('Host')}

def refs(expression):
    output=[]
    for host,service,method in re.findall(r'(TopSolid\w*Host)\.(\w+)\.(\w+)\(',expression):
        prop=next(p for p in hosttypes[host]['properties'] if p['name']==service)
        interface=prop['type'].split(',')[0]
        matches=[m for m in types[interface]['methods'] if m['name']==method]
        if not matches:raise ValueError('Missing assembly method '+interface+'.'+method)
        output.append(interface+'.'+method)
    return output

shapes={
 'none':('new JObject()',[]),
 'doc':('new JObject { ["documentId"] = Schema.Text("Document revision ID; omit to use the active document.") }',[]),
 'name':('new JObject { ["name"] = Schema.Text("Exact search name.", 256) }',['name']),
 'pdm':('new JObject { ["pdmObjectId"] = Schema.Text("PDM object ID returned by a tool.") }',['pdmObjectId']),
 'major':('new JObject { ["majorRevisionId"] = Schema.Text("Major revision ID returned by a tool.") }',['majorRevisionId']),
 'element':('new JObject { ["element"] = Schema.Element() }',['element']),
 'item':('new JObject { ["item"] = Schema.Item() }',['item']),
 'camElement':('new JObject { ["element"] = Schema.Element(), ["preparationId"] = Schema.Text("Only for a preparation ID returned by CAM; use instead of element.", 36) }',[]),
 'docname':('new JObject { ["documentId"] = Schema.Text("Document revision ID; omit for active."), ["name"] = Schema.Text("Exact system or element name.",256) }',['name']),
 'pdmname':('new JObject { ["pdmObjectId"] = Schema.Text("Working or library project PdmObjectId to search, not a folder ID."), ["name"] = Schema.Text("Exact document name.",256) }',['pdmObjectId','name'])
}
groups={};contract_rows=[]
for b in bindings:
    named_pdm=b['name'] in ('topsolid_list_projects','topsolid_list_libraries')
    named_cam=b['name'] in ('topsolid_list_cam_parts','topsolid_list_cam_tools','topsolid_list_cam_coordinate_systems','topsolid_list_cam_operations','topsolid_list_cam_scenario','topsolid_get_cam_machine') or b['name'].startswith('topsolid_list_machine_')
    cam_parameters=b['name']=='topsolid_list_cam_parameters'
    projection=', id => new JObject { ["pdmObjectId"] = id.Id, ["name"] = TopSolidHost.Pdm.GetName(id) }' if named_pdm else ''
    symbols=refs(b['expression']+projection); b['apis']=symbols
    texts=[fetch('api/'+b['module']+'/'+symbol+'.html') for symbol in symbols]
    if named_cam:
        b['description'] += ' Includes native friendly names; keep handles internal in user mode.'
        for symbol in ('TopSolid.Kernel.Automating.IElements.GetFriendlyName','TopSolid.Kernel.Automating.IElements.GetName'):
            symbols.append(symbol); texts.append(fetch('api/kernel/'+symbol+'.html'))
        if b['name'] in ('topsolid_list_cam_operations','topsolid_list_cam_scenario'):
            symbol='TopSolid.Cam.NC.Kernel.Automating.IOperations.GetDescription'
            symbols.append(symbol); texts.append(fetch('api/cam/'+symbol+'.html'))
    if cam_parameters:
        for method in ('GetName','GetFullName','GetLocalizedName','GetCategories','GetType','GetValue','IsReadOnly','ToStringValue','ToInvariantStringValue','GetEnumTypeName','GetParameterEnumValueNames','GetValueBoundValue','GetValueBoundElement','GetValueFeedRateValue','GetValueSpindleRateValue'):
            symbol='TopSolid.Cam.NC.Kernel.Automating.IParameters.'+method
            symbols.append(symbol); texts.append(fetch('api/cam/'+symbol+'.html'))
        for symbol in ('TopSolid.Cam.NC.Kernel.Automating.ParameterType','TopSolid.Cam.NC.Kernel.Automating.IOperations.GetDescription','TopSolid.Kernel.Automating.IElements.GetFriendlyName','TopSolid.Kernel.Automating.IElements.GetName'):
            symbols.append(symbol); texts.append(fetch('api/'+('cam' if '.Cam.' in symbol else 'kernel')+'/'+symbol+'.html'))
    if named_pdm:
        texts += [fetch('api/kernel/TopSolid.Kernel.Automating.'+name+'.html') for name in ('IDocuments.GetDocument','IDocuments.Exists','IDocuments.GetPdmObject','IParameters.GetCreationDateParameter','IParameters.GetDateTimeValue','IParameters.GetParameterType','IElements.Exists')]
        b['description'] += ' Names only by default. Set includeCreationDates=true only when dates are requested; unavailable dates are explicit. Never group or omit rows.'
    # Documents.GetDocument belongs to kernel even when a consuming tool later uses CAD.
    for text in texts:contract_rows.append((b['name'],text['url'],text['text']))
    props,required=shapes[b['shape']]
    if cam_parameters: props='CamParameterValues.Properties()'
    if named_pdm: props='new JObject { ["includeCreationDates"] = Schema.Boolean("Optional, default false. Query creation dates only when requested."), ["orderBy"] = Schema.Choice("Optional global name or creation-date order before pagination. Name ordering needs no dates. Missing dates block chronological ordering.", "oldestFirst", "newestFirst", "nameAscending", "nameDescending") }'
    page_default=', 100' if named_pdm or cam_parameters else ''
    if b['paged']:props='Schema.Page('+props+page_default+')'
    expr=('AutomationValues.Page('+b['expression']+', p'+projection+page_default+')' if b['paged'] else 'AutomationValues.Result('+b['expression']+')')
    if named_pdm: expr='a.ListPdmProjects('+('true' if b['name']=='topsolid_list_projects' else 'false')+', p)'
    if named_cam:
        projector='CamNames.OperationNamed' if b['name'] in ('topsolid_list_cam_operations','topsolid_list_cam_scenario') else 'CamNames.Named'
        expr='AutomationValues.Page('+b['expression']+', p, id => '+projector+'(id))' if b['paged'] else 'AutomationValues.Result(CamNames.Named('+b['expression']+'))'
    if cam_parameters: expr='CamParameterValues.Native.Page(a.CamElement(p), p)'
    code='            register(new ToolDefinition('+json.dumps(b['name'])+', '+json.dumps(b['description'])+', '+props+',\n                p => a.Read('+json.dumps(b['module'])+', () => '+expr+'), '+json.dumps(b['category'])+', new[] { '+', '.join(json.dumps(s) for s in required)+' }'+', true, new[] { '+', '.join(json.dumps(t['url']) for t in texts)+' }));'
    if not required:code=code.replace('new[] {  }','new string[0]')
    groups.setdefault(b['category'],[]).append(code)

header='''// Generated from the reviewed allowlist in scripts/ApiReference/generate_read_tools.py.
// Each call is compiled against the matched 7.20 SDK. No generic API invocation.
using System;
using Newtonsoft.Json.Linq;
using TopSolid.Automation.Mcp.Server.AddIn.Automation;
using TopSolid.Kernel.Automating;
using TopSolid.Cad.Design.Automating;
using TopSolid.Cad.Drafting.Automating;
using TopSolid.Cad.Electrode.Automating;
using TopSolid.Cam.NC.Kernel.Automating;
using TopSolid.Cae.Kernel.Automating;

namespace TopSolid.Automation.Mcp.Server.AddIn.Tools
{
'''
for category,rows in groups.items():
    name=category.replace('/','')+'ReadTools'
    code=header+'    internal static class '+name+'\n    {\n        public static void Register(AutomationGateway a, Action<ToolDefinition> register)\n        {\n'+'\n'.join(rows)+'\n        }\n    }\n}\n'
    path=ROOT/'TopSolid.Automation.Mcp.Server.AddIn/Tools'/category/(name+'.cs')
    path.parent.mkdir(parents=True,exist_ok=True);path.write_text(code,encoding='utf-8')
registration=header+'    internal static class DomainReadTools\n    {\n        public static void Register(AutomationGateway a, Action<ToolDefinition> register)\n        {\n'+''.join('            '+cat.replace('/','')+'ReadTools.Register(a, register);\n' for cat in groups)+'        }\n    }\n}\n'
(ROOT/'TopSolid.Automation.Mcp.Server.AddIn/Tools/DomainReadTools.cs').write_text(registration,encoding='utf-8')
(ROOT/'docs/api/read-tool-bindings.json').write_text(json.dumps(bindings,indent=2),encoding='utf-8')
# Full fetched contracts stay in the research cache, outside published application binaries.
(ROOT/'artifacts/api-reference/implemented-read-contracts.txt').write_text('\n\n'.join(name+'\n'+url+'\n'+text for name,url,text in contract_rows),encoding='utf-8')
print('Generated',len(bindings),'explicit read tools across',len(groups),'categories; all signatures and documentation URLs verified.')
