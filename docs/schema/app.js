

function TodoCtrl($scope, $http, $location, $anchorScroll) {
    $http.get('DataSchema.json')
        .success(function (result) {
            $scope.objectDefs = result.Ontology.OntologyGroups.OntologyGroup.ObjectDefs.ObjectDef;
            for (var i = 0; i < $scope.objectDefs.length; i++) {
            var od = $scope.objectDefs[i];
            od.$childObjects = [];
            for (var ci = 0; ci < $scope.objectDefs.length; ci++) {
                var childOD = $scope.objectDefs[ci];
                childOD.$parentObjects = childOD.$parentObjects || [];
                for (var pi = 0; pi < childOD.PropertyDefs.PropertyDef.length; pi++) {
                    var p = childOD.PropertyDefs.PropertyDef[pi];
                    if (p.Relationships && p.Relationships.Relationship) {
                        if (p.Relationships.Relationship.ReferencedObjectDef == od.Name) {
                            od.$childObjects.push(childOD);
                            childOD.$parentObjects.push(od);
                            if (p.IsCollection == 1) childOD.$isCollection = true;
                        }
                    }
                }
            }
        }
    });

    $scope.expandAll = function() {
        for (var i = 0; i < $scope.objectDefs.length; i++) {
            $scope.objectDefs[i].$expanded = true;   
        }
    }

    $scope.collapseAll = function() {
        for (var i = 0; i < $scope.objectDefs.length; i++) {
            $scope.objectDefs[i].$expanded = false;   
        }
    }

    $scope.focusObject = function(objectDef) {
        objectDef.$expanded = true;
        $location.hash(objectDef.Name);
        $anchorScroll();
    }
}

    